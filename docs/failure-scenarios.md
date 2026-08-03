# Failure Scenarios

This document records the platform’s expected behavior when authentication, authorization, message publication, delivery, processing, or Processor-to-supplier submission does not follow the normal successful path.

It describes current behavior and known limitations. Operational investigation steps are documented separately in `observability-runbook.md`.

## Invalid Login Credentials

### Scenario

A login request supplies an unknown email address, an incorrect password, or otherwise invalid credentials.

### Current Behavior

The API returns `401 Unauthorized` without revealing whether the email address exists.

No access token is issued, and no authenticated session is created in the React client.

### Observable Evidence

The client displays a generic login failure. The API should not log plaintext passwords or include submitted credentials in structured diagnostics.

### Recovery

Confirm the configured account email and password. For the bootstrap Administrator, verify that the corresponding local configuration exists and that the account was created successfully during API startup.

## Inactive Account

### Scenario

A user attempts to log in after an Administrator has deactivated the account, or attempts to use a token that was issued before deactivation.

### Current Behavior

New login attempts are rejected.

Previously issued tokens are also rejected because deactivation updates the account security stamp and the JWT validation path confirms that the account remains active and that the token’s security stamp is current.

### Recovery

An Administrator may reactivate the account. The user must then log in again to receive a new token.

## Token Invalidated by Role Change

### Scenario

An Administrator changes an account’s role while that account still holds a previously issued JWT.

### Current Behavior

The role change updates the account security stamp. The old token is rejected on later protected requests instead of continuing to carry stale authorization claims.

The user must authenticate again to receive a token containing the current role.

### Recovery

Sign in again. The React client stores tokens only in memory, so there is no refresh-token flow that silently replaces the invalidated token.

## Expired, Missing, or Invalid JWT

### Scenario

A protected request has no bearer token, has an expired token, or provides a token with an invalid signature, issuer, audience, or security stamp.

### Current Behavior

The API returns `401 Unauthorized`.

When the React client receives that response for an authenticated request, it clears the in-memory token and application state, returns to the login form, and displays a session-invalidated notice.

A valid token held by a role that lacks access to the requested operation instead receives `403 Forbidden`. The client keeps the session active and displays a readable permission message.

### Recovery

Authenticate again after a `401` response and retry with the newly issued token. A `403` response requires an Administrator to review the account’s assigned role rather than simply retrying the same token.

## Final Administrator Safeguard

### Scenario

An Administrator attempts to demote or deactivate the final active Administrator account.

### Current Behavior

The API rejects the operation and preserves at least one active Administrator.

The safeguard is enforced in the backend even if a client attempts the request directly.

### Recovery

Create or reactivate another Administrator first, then repeat the intended role or status change.

## Duplicate Account or Invalid Account Request

### Scenario

An Administrator attempts to create a duplicate email address, supplies an unsupported role, or provides a password that does not satisfy the configured Identity policy.

### Current Behavior

The API rejects the request without creating a partial account.

Account creation uses established ASP.NET Core Identity validation and password hashing. The Administration view preserves the current account list and displays the returned validation or conflict message.

### Recovery

Use a unique email address, a supported application role, and a password that satisfies the documented policy.

## Invalid Inventory Mutation

### Scenario

An Operator or Administrator submits missing or invalid inventory values, such as a non-positive configured reorder quantity.

### Current Behavior

The API rejects the request without committing a partial inventory change or starting a reorder workflow.

The React form preserves the user’s input and displays validation details returned by the API. Failed requests do not replace the current dashboard data with speculative client state.

### Recovery

Correct the reported inventory values and resubmit the request.

## Duplicate Reorder Message

### Scenario

Azure Service Bus delivers the same reorder message more than once, or two consumers attempt to process the same stable message identifier.

### Current Behavior

The Processor checks the `ProcessedMessages` ledger before performing business processing.

If the same message id and message type were already completed, the delivery is classified as `DuplicateSkipped` and completed without repeating the supplier submission or local business result.

A unique database index on message id and message type provides additional protection against concurrent duplicate processing.

The Processor also avoids repeating work when the associated reorder event already has a terminal `SupplierAccepted`, `SupplierRejected`, or legacy `Processed` status. When redelivery occurs before local completion, the same Service Bus message id is reused as the supplier idempotency key, so an already accepted supplier order is replayed rather than duplicated.

### Observable Evidence

Expected diagnostics include:

```text
reorder.outcome = duplicate-skipped
messaging.settlement = completed
```

The completion log includes the stable message id and correlation identifier.

### Recovery

No operator action is required. The duplicate message is safely completed.

### Test Coverage

Automated Processor tests verify that duplicate delivery does not create a duplicate processed-message record, repeat the local business result, or create a second supplier order.

## Transient Processing Failure

### Scenario

A valid reorder message cannot be processed successfully because of a temporary application, database, HTTP, supplier, or other dependency failure.

### Current Behavior

The Processor records a `FailedMessage` entry when the failure can be persisted. The record includes:

- message id
- message type
- failure reason
- original payload when available
- delivery attempt count
- UTC failure time

The reorder event remains `Pending`; partial supplier-state changes are not committed with the failure record.

Before `MaxDeliveryAttempts` is reached, the Worker abandons the Service Bus message so it remains available for redelivery. The next delivery resubmits the same immutable request with the same supplier idempotency key.

### Observable Evidence

Expected diagnostics include:

```text
reorder.outcome = failed
messaging.settlement = abandoned
```

Logs include the message id, correlation identifier, delivery count, and failure reason.

### Recovery

Service Bus retries the message through another delivery. If the underlying problem has been resolved, a later attempt can succeed. If the supplier had already accepted the order, the idempotent replay returns the original accepted result.

### Test Coverage

Automated Processor tests verify failed-result creation, `FailedMessage` persistence, transient supplier failure followed by successful redelivery, and safe replay after supplier acceptance. Transport settlement is verified manually through the Service Bus Emulator.

## Repeated Processing Failure

### Scenario

A valid message continues to fail through the configured maximum number of delivery attempts.

### Current Behavior

At or above `MaxDeliveryAttempts`, the Worker moves the message to the dead-letter queue instead of abandoning it again.

The dead-letter reason is:

```text
ReorderProcessingFailed
```

### Observable Evidence

Expected diagnostics include:

```text
reorder.outcome = failed
messaging.settlement = dead-lettered
```

The final log includes the message id, correlation identifier, delivery count, and failure reason.

### Recovery

An operator must inspect the dead-lettered message and the corresponding failure records before deciding whether to correct and replay the message.

The project does not currently provide an automated dead-letter replay endpoint.

## Invalid or Unsupported Payload

### Scenario

The Processor receives malformed JSON, a null message body, or content that cannot be interpreted as a valid reorder request.

### Current Behavior

The Worker dead-letters the message immediately.

Retrying malformed or unsupported content cannot make it valid, so the message does not consume the normal retry allowance.

Dead-letter reasons use the `InvalidPayload` classification.

### Observable Evidence

Expected diagnostic outcomes include:

```text
reorder.outcome = invalid-json
```

or:

```text
reorder.outcome = invalid-payload
```

with:

```text
messaging.settlement = dead-lettered
```

### Recovery

An operator must inspect the original message source and correct the producer or payload before submitting a replacement message.

## Missing or Invalid Business State

### Scenario

The message is structurally valid, but its referenced reorder event does not exist, is not `Pending`, or otherwise has a state the Processor cannot handle.

### Current Behavior

A missing event or unsupported nonterminal state produces a failed processing result. The Worker abandons the message while it remains below the delivery threshold and dead-letters it after repeated failure.

An event already in `SupplierAccepted`, `SupplierRejected`, or legacy `Processed` state is treated as terminal and the duplicate delivery is completed without repeating supplier or persistence work.

### Observable Evidence

Logs and `FailedMessages` records contain the missing event or unsupported-status explanation. Terminal duplicate handling records a duplicate-skipped outcome.

### Recovery

The underlying database state must be reviewed before replaying or replacing a genuinely invalid message. Repeated delivery alone will not repair missing or unsupported business state.

## Service Bus Publication Failure

### Scenario

The API successfully commits an inventory transition and reorder event, but the Service Bus message cannot be published.

### Current Behavior

The publication exception is logged and returned through the API request.

Because the inventory data, audit record, and reorder event are saved before message publication, the database changes may remain committed even though the caller receives an error and no message reaches the queue.

### Observable Evidence

The API logs contain the failed publication, request trace, reorder-event id, and correlation identifier.

A reorder event may remain `Pending` without a corresponding Processor receipt or completion log.

### Recovery

The pending reorder event requires investigation and manual recovery.

The project does not currently implement a transactional outbox or an automated republishing workflow.

### Known Limitation

This is an acknowledged consistency boundary in the current portfolio scope. A production implementation would commonly use a transactional outbox so the business-state change and durable publication intent are committed together.

## Invalid Supplier Order Request

### Scenario

A direct caller submits a supplier-order request with a missing, empty, duplicate, or oversized `Idempotency-Key` header, or supplies an invalid request body such as a blank SKU, non-positive requested quantity, or missing UTC trigger time.

### Current Behavior

The mock supplier API returns `400 Bad Request` and does not create an accepted supplier-order record.

The idempotency header must contain exactly one nonempty value no longer than 200 characters. The SKU is required and limited to 50 characters, `RequestedQuantity` must be positive, and `TriggeredAtUtc` must contain a nondefault UTC value.

### Observable Evidence

The response contains validation or problem-details information describing the invalid request. The supplier database should contain no order for the rejected idempotency key.

### Recovery

Correct the header or request payload and resubmit the request with the intended idempotency key.

### Test Coverage

Supplier API integration tests verify missing-header and invalid-quantity behavior. Other request-validation rules remain enforced by the supplier-owned contract and controller validation path.

## Identical Supplier Order Replay

### Scenario

A caller repeats an already accepted supplier-order request using the same idempotency key and the same business payload.

This can represent a direct safe retry, Service Bus redelivery after a transient failure, or recovery after the supplier accepted the order but the Processor could not persist the local result.

### Current Behavior

The mock supplier API returns `200 OK` with the original accepted supplier order.

The response preserves the original:

- supplier-order identifier
- acceptance time
- submitted payload
- accepted status

No additional supplier-order row is created.

The service checks for an existing accepted order before applying the configured mock behavior. A later change to delayed, transient-failure, or permanent-rejection mode therefore cannot turn an already accepted order into a simulated failure.

The Processor treats both `201 Created` and an identical `200 OK` replay as supplier acceptance and persists the returned original details.

### Observable Evidence

The repeated response contains the same `SupplierOrderId` and `AcceptedAtUtc` as the original `201 Created` response. The supplier database contains one matching row. Processor logs show the same message id, idempotency key, and correlation identifier across attempts.

### Recovery

No manual recovery action is required when redelivery can continue. The replay is the expected idempotent result.

### Test Coverage

Supplier integration tests verify identical replay, single-row persistence, and replay after the configured behavior mode changes. Processor tests verify that a simulated local-save failure after supplier acceptance can recover on redelivery without creating a second supplier order.

## Supplier Accepted but Local Persistence Fails

### Scenario

The supplier accepts a new order, but the Processor cannot commit the reorder-event update and processed-message ledger entry to the inventory database.

### Current Behavior

The supplier order remains durably accepted in the independently owned supplier database. The local reorder event remains `Pending`, and the failed attempt is recorded when the application database is available for failure persistence.

The Service Bus message remains retryable. On redelivery, the Processor sends the same payload with the same message-derived idempotency key. The supplier returns the original order with `200 OK`, allowing the Processor to complete local persistence without creating another external order.

### Observable Evidence

The supplier database contains one accepted order. The inventory database initially lacks the terminal supplier fields, and Processor diagnostics show a failed attempt followed by an accepted replay and successful completion.

### Recovery

Restore the inventory database or resolve the local persistence failure before the delivery limit is exhausted. Do not generate a replacement supplier idempotency key.

### Test Coverage

Processor workflow tests simulate a save failure after supplier acceptance and verify one unique supplier order, one final processed-message entry, and a `SupplierAccepted` reorder event after redelivery.

## Conflicting Supplier Idempotency Key

### Scenario

A caller reuses an accepted idempotency key with a different reorder event, inventory item, SKU, requested quantity, or trigger time.

### Current Behavior

The mock supplier API returns `409 Conflict` and preserves the original accepted order unchanged.

A unique database index on `IdempotencyKey` provides database-level duplicate protection. When concurrent requests race to insert the same key, the controller reloads the persisted record and applies the same identical-replay or conflicting-replay comparison.

### Observable Evidence

The response contains conflict problem details. The supplier database retains only the original accepted order.

### Recovery

Determine whether the caller reused the wrong idempotency key or changed a payload that should have remained stable. Submit a genuinely different supplier order with a new key, or retry the original payload with the existing key.

### Test Coverage

Supplier tests verify conflicting payload behavior and database-level uniqueness enforcement.

## Simulated Transient Supplier Failure

### Scenario

The mock supplier service is configured with `SupplierBehavior:Mode` set to `TransientFailure`, and the Processor submits a new supplier order.

### Current Behavior

The service returns `503 Service Unavailable` for the configured number of attempts associated with that idempotency key. The response includes:

```http
Retry-After: 1
```

No accepted supplier-order record is created during those failed attempts.

The typed client treats `503` as a technical failure. The Processor leaves the reorder event `Pending`, records a failed attempt, and the Worker abandons the Service Bus message. Redelivery reuses the same idempotency key. After the configured failures are exhausted, the supplier returns `201 Created`, the Processor persists `SupplierAccepted`, and later identical requests return `200 OK`.

Transient attempt counters are held in supplier-process memory. Restarting the supplier process resets counters for requests that have not yet been accepted. Accepted orders remain durable in SQL Server and continue to replay safely after restart.

### Observable Evidence

Supplier logs identify the simulated transient failure, idempotency key, correlation identifier, and attempt number. Processor logs show failure recording, abandonment, redelivery, supplier acceptance, and completion under the same correlation id.

### Recovery

Normally no manual action is required. Allow Service Bus redelivery after the dependency recovers. Use the same idempotency key for every attempt.

### Test Coverage

Supplier integration tests verify the configured transient failures, eventual acceptance, later replay, and single accepted database record. Processor tests verify failed-attempt persistence and successful recovery using the same idempotency key.

## Excessive Supplier Delay

### Scenario

The supplier is configured with `Delayed` behavior and waits longer than the Processor HTTP resilience pipeline’s per-attempt timeout.

### Current Behavior

A moderate delay, such as five seconds under the current local defaults, completes successfully. A longer delay that exceeds the HTTP attempt timeout is canceled and treated as a retryable technical failure.

The supplier’s `Task.Delay` observes the request cancellation token, so local debugging may surface a `TaskCanceledException` in the supplier process when the Processor times out the request. No accepted supplier-order record is created for a canceled request that did not reach persistence.

### Observable Evidence

Processor logs show an HTTP timeout or cancellation, a `FailedMessage` attempt, and message abandonment. Supplier logs may show request cancellation during the configured delay.

### Recovery

Reduce the mock delay for successful delayed-response verification, or allow Service Bus redelivery after changing the dependency behavior. Do not increase production-style timeouts solely to accommodate an intentionally excessive mock delay.

## Simulated Permanent Supplier Rejection

### Scenario

The mock supplier service is configured with `SupplierBehavior:Mode` set to `PermanentRejection`, and the Processor submits a new supplier order.

### Current Behavior

The service returns `422 Unprocessable Entity` with the configured rejection message. It does not create an accepted supplier-order record.

The typed client maps the response to a permanent rejection result. The Processor changes the reorder event to `SupplierRejected`, stores the supplier status and rejection reason, records the message in `ProcessedMessages`, and completes the Service Bus delivery.

Permanent rejection is a terminal business outcome rather than a transient infrastructure failure. It is not abandoned for repeated technical retry.

### Observable Evidence

The supplier response and logs contain the configured rejection detail and correlation identifier. The inventory reorder event contains:

- `status = SupplierRejected`
- supplier status `Rejected`
- the persisted rejection reason
- no supplier order identifier or acceptance time

The Workflow view presents the rejection reason and the Supplier Rejected summary count.

### Recovery

Review the rejected SKU, quantity, or supplier-specific business condition. A new business decision may require changing inventory configuration or starting a genuinely new reorder workflow; repeatedly delivering the same rejected order is not an appropriate recovery.

### Test Coverage

Supplier integration tests verify the `422` response and absence of an accepted order. Processor tests verify terminal rejection persistence, one processed-message record, one supplier call, and completion without retry.

## Supplier Database Unavailable

### Scenario

The mock supplier API cannot connect to its independently owned supplier database during startup, migration, health evaluation, replay lookup, or order acceptance.

### Current Behavior

The supplier service may fail startup migration or fail requests that require persistence. Its `/health` endpoint should report the dependency as unhealthy when the application is running but cannot use the database.

Processor submissions fail technically. The reorder event remains `Pending`, a failure attempt is recorded when possible, and the Service Bus message is abandoned or dead-lettered according to delivery count.

The inventory API and application database remain separate, so authenticated inventory and audit operations may continue while supplier submission is unavailable.

### Observable Evidence

Inspect:

- the supplier resource health state
- supplier startup and request logs
- Processor submission and settlement logs
- SQL Server resource health
- the `supplierdb` or Docker supplier-database connection configuration
- migration errors

The supplier `/alive` endpoint may remain available even when `/health` reports a database failure.

### Recovery

Restore supplier-database connectivity, confirm that its migration is current, and allow redelivery with the same idempotency key.

If the order may have been accepted before the caller observed the failure, an identical replay safely returns the durable accepted result rather than creating another order.

## Database Unavailable

### Scenario

The API or Processor cannot connect to the application SQL Server.

### API Behavior

Database-backed Identity, account-management, inventory, audit, and workflow operations fail.

The dashboard-oriented operations health endpoint reports the database connection as unavailable and avoids representing unavailable record counts as zero.

Depending on startup timing, the API may also be unable to apply migrations or bootstrap Identity roles and the initial Administrator.

### Processor Behavior

The Processor cannot read reorder state or persist processing results. The Worker treats the processing attempt as failed and abandons or dead-letters the message according to its delivery count.

If the database is restored before the maximum delivery count is reached, a later delivery may succeed.

### Recovery

Restore database connectivity and inspect:

- API startup and Identity bootstrap logs
- pending reorder events
- failed-message records
- Processor logs
- queue and dead-letter state

## Missing Local Authentication Configuration

### Scenario

The API starts without a valid JWT signing key or without optional bootstrap Administrator credentials.

### Current Behavior

A missing or invalid signing configuration prevents the authentication system from operating correctly and should be treated as a startup/configuration error.

When bootstrap Administrator credentials are omitted, the application can still create the standard roles, but no initial Administrator is created. This is expected in test environments that create accounts programmatically, but it prevents normal local UI administration until an Administrator exists.

### Recovery

Configure the JWT signing key and bootstrap credentials outside source control through the environment mechanism documented in the repository README, then restart the API.

Never commit real signing keys or account passwords.

## Service Bus Emulator Unavailable

### Scenario

The API or Processor cannot connect to the local Service Bus Emulator.

### Current Behavior

The API cannot publish new reorder messages, and the Processor cannot receive queued messages.

Existing Identity and database state remains available, but asynchronous workflow progress stops.

### Recovery

Restore the emulator and confirm that the API and Processor use the same queue configuration.

Detailed emulator startup troubleshooting belongs in `observability-runbook.md`.

## Frontend Data Load or Refresh Failure

### Scenario

A protected request for inventory, workflow, system-health, audit, or account data fails after the user has authenticated.

### Current Behavior

The client presents a readable error within the affected view or card instead of treating unavailable data as a successful empty result.

System Health maintains its own loading and error state, so a health refresh failure does not hide otherwise available inventory or workflow information. Workflow History has its own refresh control and reports refresh failures within the card without clearing the login session. Administrator-only Audit and Administration data is requested only for an Administrator session.

A `401 Unauthorized` response still follows the session-invalidated behavior documented above. Other request failures keep the current session active unless authentication is no longer valid.

### Recovery

Retry the affected request or use the view’s refresh control where one is available. If the failure persists, inspect the API, database, or account authorization state using the observability runbook.

## Scope Limitations

The project currently does not include:

- an external identity provider or single sign-on integration
- refresh-token rotation or persistent browser sessions
- automated password-reset or email-verification workflows
- service-to-service supplier authentication
- a transactional outbox
- automated dead-letter replay
- automatic republishing of orphaned pending reorder events
- production alerts or paging
- production telemetry retention
- durable supplier transient-attempt counters
- shipment, delivery, or physical stock-receipt workflow
- a real commercial supplier-system recovery process

These limitations are documented so the project demonstrates implemented security, idempotency, retry, supplier-integration, and recovery behavior without overstating production completeness.

