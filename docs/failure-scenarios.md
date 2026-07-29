# Failure Scenarios

This document records the platform’s expected behavior when authentication, authorization, message publication, delivery, or processing does not follow the normal successful path.

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

A valid token held by a role that lacks access to the requested operation instead receives `403 Forbidden`.

### Recovery

Authenticate again and retry the request with the returned access token. A forbidden response requires an Administrator to review the account’s assigned role rather than simply retrying the same token.

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

Account creation uses established ASP.NET Core Identity validation and password hashing. The frontend displays the returned validation or conflict message.

### Recovery

Use a unique email address, a supported application role, and a password that satisfies the documented policy.

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
- a real supplier-system recovery workflow

These limitations are documented so the project demonstrates implemented security and reliability behavior without overstating production completeness.
