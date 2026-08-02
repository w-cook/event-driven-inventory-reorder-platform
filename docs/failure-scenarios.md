# Failure Scenarios

This document records the platform’s expected behavior when authentication, authorization, message publication, delivery, processing, or direct mock-supplier submission does not follow the normal successful path.

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

If the same message id and message type were already processed, the delivery is classified as `DuplicateSkipped` and completed without repeating the reorder operation.

A unique database index on message id and message type provides additional protection against concurrent duplicate processing.

The Processor also avoids repeating the operation when the associated reorder event is already marked `Processed`.

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

Automated Processor tests verify that duplicate delivery does not create a duplicate processed-message record or repeat the business result.

## Transient Processing Failure

### Scenario

A valid reorder message cannot be processed successfully because of a temporary application or dependency failure.

### Current Behavior

The Processor records a `FailedMessage` entry when the failure can be persisted. The record includes:

- message id
- message type
- failure reason
- original payload when available
- delivery attempt count
- UTC failure time

Before `MaxDeliveryAttempts` is reached, the Worker abandons the Service Bus message so it remains available for redelivery.

### Observable Evidence

Expected diagnostics include:

```text
reorder.outcome = failed
messaging.settlement = abandoned
```

Logs include the message id, correlation identifier, delivery count, and failure reason.

### Recovery

Service Bus retries the message through another delivery. If the underlying problem has been resolved, a later attempt can succeed.

### Test Coverage

Automated Processor tests verify failed-result creation and `FailedMessage` persistence. Transport settlement is verified manually through the Service Bus Emulator.

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

The message is structurally valid, but its referenced reorder event does not exist or has a status the Processor cannot handle.

### Current Behavior

The Processor records the failure and returns a failed processing result.

The Worker abandons the message while it remains below the delivery threshold and dead-letters it after repeated failure.

### Observable Evidence

Logs and `FailedMessages` records contain the missing event or unsupported-status explanation.

### Recovery

The underlying database state must be reviewed before replaying or replacing the message. Repeated delivery alone will not repair invalid business state.

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

This can represent a safe retry after the caller did not receive or could not trust the original response.

### Current Behavior

The mock supplier API returns `200 OK` with the original accepted supplier order.

The response preserves the original:

- supplier-order identifier
- acceptance time
- submitted payload
- accepted status

No additional supplier-order row is created.

The service checks for an existing accepted order before applying the configured mock behavior. A later change to delayed, transient-failure, or permanent-rejection mode therefore cannot turn an already accepted order into a simulated failure.

### Observable Evidence

The repeated response contains the same `SupplierOrderId` and `AcceptedAtUtc` as the original `201 Created` response. The supplier database contains one matching row.

### Recovery

No recovery action is required. The replay is the expected idempotent result.

### Test Coverage

Supplier integration tests verify identical replay, single-row persistence, and replay after the configured behavior mode changes.

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

The mock supplier service is configured with `SupplierBehavior:Mode` set to `TransientFailure`, and a direct caller submits a new supplier order.

### Current Behavior

The service returns `503 Service Unavailable` for the configured number of attempts associated with that idempotency key. The response includes:

```http
Retry-After: 1
```

No accepted supplier-order record is created during those failed attempts. A later attempt proceeds normally and returns `201 Created`; another identical replay then returns `200 OK`.

Transient attempt counters are held in supplier-process memory. Restarting the supplier process resets counters for requests that have not yet been accepted. Accepted orders remain durable in SQL Server and continue to replay safely after restart.

During Phase 10, this behavior affects only direct supplier callers and supplier integration tests. The Processor does not call the supplier service until Phase 11.

### Observable Evidence

Supplier logs identify the simulated transient failure, idempotency key, and attempt number. Repeated direct requests show the configured sequence of `503`, eventual `201`, and idempotent `200` responses.

### Recovery

For direct verification, retry the same request and idempotency key after the indicated delay. A caller should not generate a new key merely because a response is temporarily unavailable.

Processor-driven retry and Service Bus redelivery behavior for this failure will be implemented in Phase 11.

### Test Coverage

Supplier integration tests verify the configured transient failures, eventual acceptance, later replay, and single accepted database record.

## Simulated Permanent Supplier Rejection

### Scenario

The mock supplier service is configured with `SupplierBehavior:Mode` set to `PermanentRejection`, and a direct caller submits a new supplier order.

### Current Behavior

The service returns `422 Unprocessable Entity` with the configured rejection message. It does not create an accepted supplier-order record.

During Phase 10, this response does not change inventory-platform reorder state because the Processor is not yet connected to the supplier boundary.

### Observable Evidence

The response title identifies a supplier-order rejection and its detail contains the configured message. Supplier logs include the rejected idempotency key, and the supplier database contains no accepted order for that key.

### Recovery

Review the request and configured mock mode. A permanent rejection should not be retried indefinitely as though it were temporary.

The Phase 11 workflow will distinguish permanent rejection from retryable supplier failure and persist the corresponding reorder outcome.

### Test Coverage

Supplier integration tests verify the `422` response, configured rejection message, and absence of a persisted accepted order.

## Supplier Database Unavailable

### Scenario

The mock supplier API cannot connect to its independently owned supplier database during startup, migration, health evaluation, or order acceptance.

### Current Behavior

The supplier service may fail startup migration or fail requests that require persistence. Its `/health` endpoint should report the dependency as unhealthy when the application is running but cannot use the database.

The inventory API and current Processor workflow use a separate application database and remain architecturally independent of the supplier database during Phase 10. A supplier-database outage therefore does not by itself change inventory-platform reorder events until Phase 11 connects the Processor to the supplier.

### Observable Evidence

Inspect:

- the supplier resource health state
- supplier startup and request logs
- SQL Server resource health
- the `supplierdb` or Docker supplier-database connection configuration
- migration errors

The supplier `/alive` endpoint may remain available even when `/health` reports a database failure.

### Recovery

Restore supplier-database connectivity, confirm that its migration is current, and retry the original request with the same idempotency key.

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

System Health maintains its own loading and error state, so a health refresh failure does not hide otherwise available inventory or workflow information. Administrator-only Audit and Administration data is requested only for an Administrator session.

A `401 Unauthorized` response still follows the session-invalidated behavior documented above. Other request failures keep the current session active unless authentication is no longer valid.

### Recovery

Retry the affected request or use the view’s refresh control where one is available. If the failure persists, inspect the API, database, or account authorization state using the observability runbook.

## Scope Limitations

The project currently does not include:

- an external identity provider or single sign-on integration
- refresh-token rotation or persistent browser sessions
- automated password-reset or email-verification workflows
- a transactional outbox
- automated dead-letter replay
- automatic republishing of orphaned pending reorder events
- production alerts or paging
- production telemetry retention
- Processor-to-supplier retry and recovery orchestration
- a real commercial supplier-system recovery workflow

These limitations are documented so the project demonstrates implemented security and reliability behavior without overstating production completeness.
