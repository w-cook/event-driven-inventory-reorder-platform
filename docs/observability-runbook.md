# Observability Runbook

Use this runbook to trace an authenticated reorder workflow across the React client or structured API request file, ASP.NET Core API, Service Bus, Processor, typed supplier client, and independently hosted mock supplier service.

For expected system behavior during authentication failures, token invalidation, duplicate delivery, retries, dead-lettering, publication failures, supplier rejection, and dependency outages, see `failure-scenarios.md`.

The complete correlated workflow now extends from the inventory API through the queue and Processor to the supplier HTTP endpoint and back to local persistence and Service Bus settlement.

## Prerequisites

Configure the local API authentication values described in the repository README:

- JWT signing key
- bootstrap Administrator email
- bootstrap Administrator password
- manual `.http` test-account password

These values must remain outside source control.

Start the Service Bus Emulator:

```bash
docker compose -f docker-compose.local.yml up -d sb-emulator-sql servicebus-emulator
```

Start the Aspire application:

```bash
dotnet run --project InventoryReorderPlatform.AppHost
```

Wait for these Aspire resources to become available:

- `sql`
- `inventorydb`
- `supplierdb`
- `api`
- `supplier`
- `processor`
- `client`

Open the React client and confirm that the bootstrap Administrator can sign in.

## Authenticate the Structured Request Workflow

Open:

```text
InventoryReorderPlatform.Api/InventoryReorderPlatform.Api.http
```

The file is configured for Aspire mode by default and uses the `local` HTTP environment.

Before running protected requests:

1. select the `local` HTTP environment
2. run the named Administrator login request
3. create the Viewer and Operator test accounts when using an empty database
4. run the named Viewer and Operator login requests
5. execute later requests from top to bottom

The file reuses `accessToken` values from named login responses. It does not store plaintext passwords or committed bearer tokens.

If an account role or activation state changes, any token issued before that change is intentionally invalid. Run the corresponding login request again to obtain a current token.

## Trigger a Correlated Reorder Workflow

Trigger a workflow through either the React client or the structured request file.

### React client

1. Sign in as an Operator or Administrator.
2. Open the **Inventory** view.
3. Create an item at or below its reorder threshold, or edit an active item so it crosses into a low-stock state.
4. Open the **Workflow** view and note the resulting reorder event and requested quantity.
5. Use **Refresh workflow** until the supplier outcome appears.

The browser request receives an API-generated correlation identifier unless a caller supplies one explicitly. Use the Aspire trace view or API logs to locate the corresponding inventory request.

### Structured request file

Use an authenticated request that causes an inventory item to enter `ReorderPending`. Message-producing requests include explicit correlation headers such as:

```http
X-Correlation-Id: {{initialReorderCorrelationId}}
Authorization: Bearer {{operatorLogin.response.body.$.accessToken}}
```

The `.http` workflow assumes an empty database and top-to-bottom execution when using fixed identifiers and expected counts.

Confirm that:

- authentication succeeds before the inventory request
- the API request succeeds
- the response includes the expected `X-Correlation-Id`
- a reorder event is created with the configured requested quantity
- the event eventually becomes `SupplierAccepted` or `SupplierRejected`
- accepted events include the supplier order identifier, status, and acceptance time
- rejected events include the supplier rejection reason
- processing does not increase `QuantityOnHand`

A `401 Unauthorized` response indicates a missing, expired, invalidated, or otherwise invalid token. A `403 Forbidden` response indicates that the authenticated account lacks the required role.

## Search the API Logs

In the Aspire dashboard:

1. Open the API resource.
2. Open its logs.
3. Search for the correlation identifier used in the request.

Confirm that the publication entry includes:

- correlation identifier
- stable Service Bus message id
- reorder-event id

A successful publication log should contain wording similar to:

```text
Published reorder message
```

If the request created a reorder event but no publication log appears, inspect the API error logs for a publication failure.

Do not expect logs to contain plaintext passwords, JWT signing keys, password hashes, or raw bearer tokens. Authentication troubleshooting should use status codes, account state, and safe structured metadata rather than sensitive values.

## Search the Processor Logs

Open the Processor logs and search for the same correlation identifier.

A successful accepted workflow should contain entries similar to:

```text
Received reorder message
Supplier accepted order
Handled reorder message ... SupplierAccepted
Completed supplier-accepted message
```

A permanent rejection should contain:

```text
Supplier permanently rejected reorder event
Handled reorder message ... SupplierRejected
Completed permanently rejected message
```

Confirm that the entries share:

- the same correlation identifier
- the same stable message id
- the expected reorder-event id
- the supplier idempotency key derived from the message id

Other possible lifecycle entries include:

```text
Completed duplicate message
Abandoned message
Dead-lettered message
```

The meaning and recovery expectations for those outcomes are documented in `failure-scenarios.md`.

## Search the Supplier Logs

Open the `supplier` resource logs and search for the same correlation identifier.

For an accepted workflow, confirm that the supplier log includes:

- accepted supplier order identifier
- reorder-event identifier
- idempotency key
- correlation identifier

For simulated transient failure, confirm that the log includes the attempt number and same idempotency key. For permanent rejection, confirm that the configured rejection is logged under the same correlation identifier.

The Processor typed-client log and supplier-service log should both appear in the global Aspire log search, making the HTTP boundary visible from both sides.

## Inspect the Distributed Trace

Open the Aspire **Traces** view.

Locate the related inventory request by searching for either:

```text
POST /api/inventoryitems
```

```text
PUT /api/inventoryitems
```

or the custom workflow activity:

```text
PublishReorderMessage
```

The application-owned trace flow should resemble:

```text
POST or PUT /api/inventoryitems
└── PublishReorderMessage
    └── ProcessReorderMessage
        └── POST /api/supplier-orders
```

Depending on instrumentation and timing, the display may contain additional framework, SQL, messaging, or HTTP spans.

The preceding login request appears as a separate HTTP trace. It is not part of the asynchronous reorder trace because authentication and the later inventory mutation are separate requests.

## Inspect Trace Attributes

Open the producer and consumer activities and inspect the available attributes.

Relevant attributes include:

```text
correlation.id
messaging.system
messaging.destination.name
messaging.message.id
messaging.operation.name
messaging.delivery.count
inventory.item.id
reorder.event.id
reorder.outcome
messaging.settlement
http.request.method
server.address
http.response.status_code
```

Not every attribute applies to every span.

The producer activity should identify the publication operation and message destination.

The consumer activity should identify the supplier result and Service Bus settlement outcome. The child HTTP span should identify the supplier request and response status.

Sensitive authentication material must not be added as trace attributes.

## Verify Health

Use the Aspire resource health indicators for infrastructure-level status.

The shared service defaults also expose:

```http
GET /health
GET /alive
```

For dashboard-oriented application status, use an authenticated request:

```http
GET /api/operations/health
Authorization: Bearer {{viewerLogin.response.body.$.accessToken}}
```

Confirm that the operations response reports:

- API status
- database connectivity
- inventory-item count
- reorder-event count
- UTC check time

The Dashboard’s System Health card performs this request with the current in-memory bearer token and can refresh independently of the summary metrics.

### Verify the mock supplier resource

In Aspire, open the `supplier` resource and confirm that it and `supplierdb` are healthy.

The supplier service exposes:

```http
GET /health
GET /alive
GET /openapi/v1.json
```

In Docker/local mode, use:

```text
http://localhost:8082/health
http://localhost:8082/alive
http://localhost:8082/openapi/v1.json
```

`/health` includes dependency health, while `/alive` confirms that the supplier process is running. A supplier process may therefore be alive while its database-dependent health check is unhealthy.

For integrated supplier verification, create a low-stock item and confirm:

- the Processor sends the stable message id as `Idempotency-Key`
- the Processor sends the workflow `X-Correlation-Id`
- a normal new request returns `201 Created`
- the inventory reorder event becomes `SupplierAccepted`
- the event stores the supplier order identifier, status, and acceptance time
- the supplier database contains one accepted order
- `QuantityOnHand` remains unchanged

For direct idempotency verification, repeat an accepted request with the same key and payload. Confirm that `200 OK` returns the original supplier order and no additional row is created.

Review supplier logs for acceptance, replay, simulated transient failure, or permanent rejection under the same correlation identifier used by the inventory workflow.

## Verify Audit Records

Sign in as an Administrator and open the **Audit** view, or call:

```http
GET /api/audit-records
Authorization: Bearer <administrator-access-token>
```

Confirm that a successful inventory creation or update produces a newest-first record containing:

- acting user and role
- inventory action
- affected entity identifier
- UTC occurrence time
- action-specific details, including previous and current values for updates

Account creation, role changes, and activation changes should produce corresponding records. Rejected authorization or validation attempts are not recorded as completed business actions.

## Verify Authentication and Account State

### Confirm login

Run:

```http
POST /api/auth/login
```

Expected successful behavior:

- `200 OK`
- an access token is returned
- the expected role appears in the response
- the React client shows the signed-in email and role

Invalid credentials should produce `401 Unauthorized` without revealing whether the email exists.

### Confirm token invalidation

To verify security-stamp invalidation:

1. sign in as a non-current test account
2. retain its access token
3. use an Administrator session to change that account’s role or activation state
4. retry a protected request with the old token

Expected result:

```text
401 Unauthorized
```

Log in again after reactivation or a role change to receive a token with the current account state.

### Confirm role enforcement

Expected examples:

- Viewer inventory read: `200 OK`
- Viewer inventory create/update: `403 Forbidden`
- Operator inventory create/update: success when the request is valid
- Operator audit or account-management access: `403 Forbidden`
- Administrator audit and account-management access: success when the request is valid

## Troubleshooting

### API Starts and Then Exits

Inspect the API logs for:

- missing or invalid JWT signing configuration
- database migration failures
- Identity bootstrap failures
- dependency-injection validation errors

A singleton service cannot depend directly on a scoped service. Correlation services used by the singleton message publisher must have compatible lifetimes.

After correcting the configuration or registration, run:

```bash
dotnet build
dotnet test
```

Then restart the AppHost.

### Bootstrap Administrator Cannot Log In

Confirm that:

- the bootstrap email and password are configured for the API project
- the password satisfies the configured Identity policy
- the application SQL database is available
- startup logs indicate that Identity roles were created
- the Administrator account is active

Bootstrap configuration creates the initial account only when needed. Changing the configured password later does not automatically reset an existing account’s stored password.

For a disposable local environment, a full database reset can recreate the bootstrap account, but do not remove volumes unless losing local data is acceptable.

### Protected Request Returns 401

Confirm that:

- a login request succeeded
- the bearer header contains the returned access token
- the token has not expired
- the account is still active
- the account role or status has not changed since the token was issued
- the API is using the same issuer, audience, and signing configuration that issued the token

In the `.http` file, rerun the appropriate named login request.

In the React client, a rejected authenticated request clears the in-memory session and returns to the login form automatically. Sign in again after confirming the account is active and correctly assigned.

### Protected Request Returns 403

The token is valid, but its role does not satisfy the endpoint policy.

Confirm that the account has the expected Viewer, Operator, or Administrator role. An Administrator role change invalidates the old token, so log in again before retesting.

### Account Management Change Is Rejected

For role or status changes, confirm that the operation would not demote or deactivate the final active Administrator.

In the **Administration** view, confirm:

- the email is unique
- the role is supported
- the password satisfies the Identity policy

### API Logs Appear but Processor Logs Do Not

Confirm that:

- the Service Bus Emulator is running
- the Processor resource is healthy
- the API and Processor use the same queue name
- the request actually caused a transition into `ReorderPending`

Updating an item that is already `ReorderPending` does not create another reorder event or publish another message.

### Correlation Appears Only in Some Logs

Search using the complete correlation value.

Important lifecycle entries include the correlation identifier directly in their message templates. Other entries may expose it only through structured logging-scope properties.

Open the log-entry details and inspect its structured fields when necessary.

### Producer and Consumer Spans Are Disconnected

Confirm that the Service Bus message contains:

```text
traceparent
```

and, when present:

```text
tracestate
```

Confirm that the Processor reconstructs its parent context from the incoming `traceparent` value before starting `ProcessReorderMessage`.

### Custom Workflow Spans Do Not Appear

Confirm that the shared OpenTelemetry configuration registers:

```text
InventoryReorderPlatform.Workflow
```

through `AddSource`.

Also confirm that the API and Processor are exporting telemetry to the Aspire dashboard.

### Reorder Event Remains Pending

Search the API logs for the correlation identifier.

Then check for:

1. a successful publication log
2. a Processor receipt log
3. a supplier-client submission log
4. a supplier-service response or failure log
5. a Processor persistence and settlement log

This sequence helps determine whether the workflow stopped during publication, delivery, supplier submission, local persistence, or settlement.

A retryable supplier or database failure intentionally leaves the event `Pending`. Use the Workflow History refresh control after the next Service Bus delivery completes.

See `failure-scenarios.md` for the expected behavior of each failure category.

### Supplier Resource Is Unhealthy

Confirm that:

- the `supplier` process is running
- `supplierdb` is available
- the supplier connection string uses the correct database name
- the supplier migration applied successfully
- the configured behavior options pass startup validation

Inspect the supplier resource logs and SQL Server resource logs. Use `/alive` to distinguish a running process from a healthy database dependency.

In Docker/local mode, inspect:

```bash
docker compose -f docker-compose.local.yml logs supplier
docker compose -f docker-compose.local.yml logs app-sql
```

### Supplier Returns 503 Service Unavailable

A `503` response is expected when `SupplierBehavior:Mode` is `TransientFailure` and the configured number of failures has not yet been exhausted.

Confirm the response includes:

```http
Retry-After: 1
```

The typed client treats the response as a technical failure. The Processor records a failed attempt, leaves the reorder event `Pending`, and the Worker abandons the Service Bus message. Redelivery uses the same payload and idempotency key.

Transient-attempt counters are process-local and reset when the supplier restarts. Accepted orders are durable and continue to replay from SQL Server.

Search all logs using the correlation identifier to confirm the sequence of `503`, abandonment, redelivery, eventual acceptance, and completion.

### Delayed Supplier Request Is Canceled

A moderate configured delay should complete before the HTTP attempt timeout. An excessive delay can cause the Processor to cancel the request.

The supplier may surface `TaskCanceledException` from its delay because the request cancellation token was triggered by the caller timeout. Treat this as a retryable technical failure rather than a supplier business rejection.

For successful delayed-response verification, use a delay below the current HTTP attempt timeout, such as five seconds. A longer delay can be used deliberately to verify timeout, failure recording, and redelivery behavior.

### Supplier Returns 422 Unprocessable Entity

A `422` response is expected when `SupplierBehavior:Mode` is `PermanentRejection`.

Inspect the problem-details response and configured rejection message. Confirm that no accepted supplier-order row was created.

The Processor should persist:

```text
Status = SupplierRejected
SupplierOrderStatus = Rejected
SupplierRejectionReason = <configured detail>
```

The Service Bus message should be completed rather than abandoned because permanent rejection is a handled terminal business outcome. Refresh Workflow History to view the rejection.

### Supplier Replay Returns 409 Conflict

A `409 Conflict` means an existing idempotency key was reused with a different business payload.

Compare the repeated request with the original accepted request, including:

- reorder-event id
- inventory-item id
- SKU
- requested quantity
- trigger time

Retry the original payload with the existing key, or use a new key only for a genuinely different supplier order.

### Supplier Does Not Appear in the Reorder Trace

This is no longer expected for a successful current workflow.

The distributed trace should include:

```text
Inventory API
└── PublishReorderMessage
    └── ProcessReorderMessage
        └── POST /api/supplier-orders
```

If the supplier span is missing, confirm that:

- the Processor is using the registered typed `HttpClient`
- shared HTTP client instrumentation is enabled
- the Processor resolves the configured supplier base URL
- the supplier request was actually attempted
- the consumer activity remained current while the HTTP request was sent
- the Processor and supplier export telemetry to the Aspire dashboard

Use correlated structured logs to distinguish a missing trace export from a missing supplier request.

### Service Bus Emulator Does Not Become Ready

Inspect:

```bash
docker compose -f docker-compose.local.yml logs servicebus-emulator
docker compose -f docker-compose.local.yml logs sb-emulator-sql
```

A stale Service Bus Emulator database file may prevent initialization.

Remove only the emulator-related containers:

```bash
docker compose -f docker-compose.local.yml rm -s -f servicebus-ready servicebus-emulator sb-emulator-sql
```

Then restart the stack.

Avoid removing the application SQL volume unless a complete application-data reset is intended.

## Shutdown

Stop the Aspire AppHost with `Ctrl+C`.

Then stop the local containers:

```bash
docker compose -f docker-compose.local.yml down
```

Do not add `-v` unless a full data reset is intended.
