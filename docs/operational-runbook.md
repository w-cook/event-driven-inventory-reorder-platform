# Operational Runbook

## Purpose and Scope

This runbook is the primary operating guide for the Event-Driven Inventory Reorder Platform. It covers local configuration, startup, authentication, health verification, normal workflow verification, supplier simulation, OpenAPI inspection, shutdown, and disposable-data reset procedures.

The platform is a local, reproducible internal business application composed of:

- an ASP.NET Core inventory API
- ASP.NET Core Identity and JWT bearer authentication
- a background Processor
- Azure Service Bus-compatible queue messaging through the local emulator
- an independently hosted mock supplier API
- separate inventory and supplier SQL Server databases
- a React/TypeScript operations client
- .NET Aspire health, logs, metrics, and distributed traces

This document focuses on normal operation. Use the linked specialist documents for detailed endpoint contracts, failure behavior, architecture, and diagnostics.

## Prerequisites

Install:

- .NET 10 SDK
- Node.js and npm
- Docker Desktop or another Docker Compose-compatible environment
- Git

Install frontend dependencies once from the repository root:

```bash
cd client
npm install
cd ..
```

Before running the application after a code change, verify the repository baseline:

```bash
dotnet build
dotnet test

cd client
npm run lint
npm run build
cd ..
```

## Local Configuration and Secrets

The API requires a JWT signing key and bootstrap Administrator credentials. These values must remain outside source control.

### Docker/local configuration

Copy the tracked placeholder file:

```cmd
copy .env.example .env
```

Set local values in `.env`:

```dotenv
JWT_SIGNING_KEY=<long-random-local-development-key>
BOOTSTRAP_ADMIN_EMAIL=<local-administrator-email>
BOOTSTRAP_ADMIN_PASSWORD=<strong-local-administrator-password>
```

The tracked `.env.example` contains placeholders only. The real `.env` file is excluded from source control.

### Aspire configuration

Configure API secrets with ASP.NET Core User Secrets:

```bash
dotnet user-secrets set "Jwt:SigningKey" "<long-random-local-signing-key>" --project InventoryReorderPlatform.Api
dotnet user-secrets set "BootstrapAdmin:Email" "<local-administrator-email>" --project InventoryReorderPlatform.Api
dotnet user-secrets set "BootstrapAdmin:Password" "<strong-local-administrator-password>" --project InventoryReorderPlatform.Api
dotnet user-secrets set "HttpTesting:AccountPassword" "<strong-local-test-account-password>" --project InventoryReorderPlatform.Api
```

The Administrator and manual-test passwords must satisfy the configured Identity policy:

- at least 10 characters
- at least one uppercase letter
- at least one lowercase letter
- at least one number
- at least one non-alphanumeric character

### Secret-handling rules

Never commit:

- JWT signing keys
- passwords
- bearer tokens
- password hashes
- real connection strings or cloud credentials
- populated `.env` or frontend environment files containing secrets

Authentication credentials belong to API configuration. Do not place them in `client/.env.local`.

The SQL Server password and Service Bus emulator key values committed in `docker-compose.local.yml` are disposable local-development defaults. They are not production credentials and must not be presented or reused as such.

## Run with .NET Aspire

Aspire is the preferred mode for normal development, resource health, structured logs, metrics, and distributed traces.

Aspire runs:

- `sql`
- `inventorydb`
- `supplierdb`
- `api`
- `supplier`
- `processor`
- `client`

The Service Bus Emulator and its SQL dependency remain external Docker Compose services.

### 1. Start the Service Bus Emulator

From the repository root:

```bash
docker compose -f docker-compose.local.yml up -d sb-emulator-sql servicebus-emulator
```

Confirm the containers are running:

```bash
docker compose -f docker-compose.local.yml ps
```

The emulator health endpoint is available at:

```text
http://localhost:5300/health
```

### 2. Start the Aspire AppHost

```bash
dotnet run --project InventoryReorderPlatform.AppHost
```

Open the Aspire dashboard URL printed in the terminal.

Wait for the application resources to become healthy or running. The `client` resource starts automatically, and Aspire provides the current API endpoint to Vite. No manual frontend proxy change is required in this mode.

### 3. Open the application

Open the `client` endpoint from the Aspire dashboard and sign in with the configured bootstrap Administrator account.

Aspire assigns dynamic service addresses. Use the dashboard links for the API, supplier API, health endpoints, and OpenAPI documents rather than assuming the Docker/local ports.

### 4. Stop Aspire mode

Stop the AppHost:

```text
Ctrl+C
```

Then stop the external emulator services:

```bash
docker compose -f docker-compose.local.yml stop servicebus-emulator sb-emulator-sql
```

To remove the stopped emulator containers and Compose network:

```bash
docker compose -f docker-compose.local.yml down
```

Do not add `-v` unless a destructive local-data reset is intended.

## Run with Docker Compose

Docker/local mode runs the backend and infrastructure as a Compose stack:

- application SQL Server
- inventory application database
- supplier database
- Service Bus Emulator
- Service Bus Emulator SQL dependency
- Service Bus readiness check
- inventory API
- mock supplier API
- Processor

The React client runs separately through Vite.

### 1. Start the backend stack

From the repository root:

```bash
docker compose -f docker-compose.local.yml up -d --build
docker compose -f docker-compose.local.yml ps
```

### 2. Start the React client

In a second terminal:

```bash
cd client
npm run dev
```

### 3. Use the stable local addresses

| Resource | Address |
| --- | --- |
| React client | `http://localhost:5173` |
| Inventory API | `http://localhost:8080` |
| Inventory API health | `http://localhost:8080/health` |
| Inventory API liveness | `http://localhost:8080/alive` |
| Inventory API OpenAPI | `http://localhost:8080/openapi/v1.json` |
| Mock supplier API | `http://localhost:8082` |
| Supplier health | `http://localhost:8082/health` |
| Supplier liveness | `http://localhost:8082/alive` |
| Supplier OpenAPI | `http://localhost:8082/openapi/v1.json` |
| Service Bus Emulator health | `http://localhost:5300/health` |
| Application SQL Server | `localhost,14333` |

Outside Aspire, Vite proxies relative `/api` requests to `http://localhost:8080` by default.

Optional frontend settings may be placed in `client/.env.local`:

```dotenv
VITE_API_PROXY_TARGET=http://localhost:8080
VITE_PORT=5173
```

### 4. Inspect service status and logs

```bash
docker compose -f docker-compose.local.yml ps
docker compose -f docker-compose.local.yml logs -f api
docker compose -f docker-compose.local.yml logs -f supplier
docker compose -f docker-compose.local.yml logs -f processor
docker compose -f docker-compose.local.yml logs servicebus-emulator
```

## Bootstrap Administrator

At API startup, the application creates the Viewer, Operator, and Administrator roles. When valid bootstrap credentials are configured, it creates the initial Administrator only when that account does not already exist.

There is no public registration endpoint. After bootstrap, an authenticated Administrator creates and manages additional accounts through the Administration view or the account-management API.

Important behavior:

- changing `BOOTSTRAP_ADMIN_PASSWORD` or the corresponding User Secret does not reset the password of an existing account
- changing an existing account’s role or active state invalidates previously issued JWTs for that account
- the final active Administrator cannot be demoted or deactivated
- recreating the bootstrap account requires resetting disposable application data

## Authenticate and Use a JWT

### Login

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "admin@example.local",
  "password": "<local-password>"
}
```

A successful response returns:

- a signed JWT access token
- its UTC expiration time
- the authenticated user ID and email
- assigned roles

### Call a protected endpoint

```http
GET /api/inventoryitems
Authorization: Bearer <access-token>
Accept: application/json
```

The React client keeps the token only in memory. Refreshing or closing the page clears the session and requires another login.

### Authentication outcomes

| Status | Meaning |
| --- | --- |
| `401 Unauthorized` | The token is missing, expired, invalid, or invalidated, or login credentials are not accepted |
| `403 Forbidden` | The token is valid, but the account’s role does not satisfy the endpoint policy |

After an Administrator changes an account’s role or active status, that user must log in again before making protected requests.

### Application roles

| Role | Access |
| --- | --- |
| `Viewer` | Read inventory, workflow history, and operations health |
| `Operator` | Viewer access plus inventory creation and updates |
| `Administrator` | Operator access plus audit review and account administration |

For repeatable direct API verification, use:

```text
InventoryReorderPlatform.Api/InventoryReorderPlatform.Api.http
```

The structured request file is configured for Aspire mode by default and resolves test credentials from local API configuration rather than committed plaintext values.

## Verify Application Health

The platform exposes three distinct health surfaces.

### Infrastructure and dependency health

```http
GET /health
```

This reports registered dependency health checks. A process can be running while `/health` is unhealthy because a dependency is unavailable.

### Process liveness

```http
GET /alive
```

This confirms that the process is running without requiring every dependency to be healthy.

### Authenticated operations health

```http
GET /api/operations/health
Authorization: Bearer <viewer-operator-or-administrator-token>
```

This endpoint returns:

- application status
- database connectivity status
- inventory-item count when the database is available
- reorder-event count when the database is available
- UTC check time

Expected results:

- `200 OK` with `Healthy` and `Connected` when the application database is reachable
- `503 Service Unavailable` with `Unhealthy` and `Unavailable` when the database cannot be queried

In the React client, the Dashboard System Health card calls this authenticated endpoint with the current in-memory JWT.

## Verify the Normal Reorder Workflow

Use the default supplier behavior mode, `Normal`.

1. Sign in as an Operator or Administrator.
2. Open the Inventory view.
3. Create an item whose `quantityOnHand` is at or below its `reorderThreshold`, or update an active item so it crosses into that state.
4. Confirm the inventory item enters `ReorderPending`.
5. Confirm a new reorder event is created with status `Pending`.
6. Confirm `requestedQuantity` matches the item’s configured `reorderQuantity` at the time the workflow began.
7. Confirm the API publishes a stable message ID in the form `reorder-event-<ReorderEventId>`.
8. Confirm the Processor submits the supplier order using that message ID as `Idempotency-Key`.
9. Confirm the reorder event becomes `SupplierAccepted`.
10. Confirm the event stores the supplier order ID, supplier status, and UTC acceptance time.
11. Confirm `QuantityOnHand` is unchanged. Supplier acceptance does not represent physical stock receipt.
12. Confirm the supplier database contains one accepted order for the idempotency key.
13. Repeat the identical supplier request with the same key and payload.
14. Confirm the supplier returns the original order with `200 OK` and does not create another row.

Use the Workflow view’s refresh action to reload inventory and reorder-event data without clearing the current login session.

For correlation and trace verification, follow the [Observability Runbook](observability-runbook.md).

## Configure Supplier Simulation Modes

Supplier behavior is read from the `SupplierBehavior` configuration section.

```json
{
  "SupplierBehavior": {
    "Mode": "Normal",
    "DelayMilliseconds": 1500,
    "TransientFailuresBeforeSuccess": 2,
    "PermanentRejectionMessage": "The supplier rejected the requested order."
  }
}
```

Docker/local environment-variable names are:

```dotenv
SupplierBehavior__Mode=Normal
SupplierBehavior__DelayMilliseconds=1500
SupplierBehavior__TransientFailuresBeforeSuccess=2
SupplierBehavior__PermanentRejectionMessage=The supplier rejected the requested order.
```

Apply behavior changes through local supplier configuration, then restart the affected supplier service or local environment.

| Mode | Expected behavior |
| --- | --- |
| `Normal` | A valid new order is accepted with `201 Created` |
| `Delayed` | The request waits for `DelayMilliseconds`, then is accepted when the delay remains below the Processor HTTP timeout |
| `TransientFailure` | The supplier returns `503 Service Unavailable`; the Processor records the failed attempt and Service Bus redelivery retries with the same idempotency key |
| `PermanentRejection` | The supplier returns `422 Unprocessable Entity`; the reorder event becomes `SupplierRejected`, the rejection reason is stored, and the Service Bus message is completed |

Additional rules:

- delayed mode accepts values from 0 through 30,000 milliseconds
- a moderate delay such as five seconds demonstrates a slow successful response
- a delay beyond the HTTP attempt timeout is treated as a retryable technical failure
- transient failures return `Retry-After: 1`
- transient attempt counters are process-local and reset when the supplier restarts
- accepted supplier orders are durable and continue to replay safely after restart
- transient failures and permanent rejections do not create accepted supplier-order rows
- the committed Docker/local default is `Normal`

For exact contracts and verification details, see [Mock Supplier Service](mock-supplier-service.md).

## Inspect OpenAPI Documents

OpenAPI documents are exposed in Development mode.

### Aspire

Open each resource’s dynamically assigned endpoint from the Aspire dashboard and append:

```text
/openapi/v1.json
```

### Docker/local

```text
Inventory API: http://localhost:8080/openapi/v1.json
Supplier API:  http://localhost:8082/openapi/v1.json
```

Verify that:

- the inventory document contains all controller routes from `api-reference.md`
- protected inventory operations reference the JWT bearer scheme
- `POST /api/auth/login` remains anonymous
- request schemas expose their validation constraints
- documented response status codes are present
- the supplier operation exposes `Idempotency-Key` and `X-Correlation-Id`
- the practical schema examples contain placeholders rather than real credentials or tokens

## Stop the Environment

### Aspire

Stop the AppHost:

```text
Ctrl+C
```

Stop the external emulator services if they are no longer needed:

```bash
docker compose -f docker-compose.local.yml stop servicebus-emulator sb-emulator-sql
```

### Docker/local

Stop the Vite development server with `Ctrl+C` in its terminal.

Stop and remove the backend containers and Compose network:

```bash
docker compose -f docker-compose.local.yml down
```

This preserves local named volumes unless `-v` is supplied.

## Reset Disposable Local Data

For a full Docker/local reset:

```bash
docker compose -f docker-compose.local.yml down -v
docker compose -f docker-compose.local.yml up -d --build
```

The `-v` option is destructive. It removes Compose-managed local database and emulator volumes. Use it only when losing local inventory, Identity, audit, workflow, supplier-order, and messaging data is acceptable.

After a reset:

1. confirm `.env` still contains valid local values
2. restart the backend stack
3. restart the React client separately
4. allow database migrations to complete
5. sign in with the configured bootstrap Administrator
6. recreate any Viewer or Operator test accounts required for manual verification

Do not use a destructive reset as a substitute for diagnosing a persistent production-style failure.

## Troubleshooting and Related Documentation

### API starts and exits

Check API logs for:

- missing or invalid JWT configuration
- database migration failures
- Identity role or bootstrap failures
- dependency-injection validation failures

After correcting the issue:

```bash
dotnet build
dotnet test
```

Then restart the environment.

### Bootstrap Administrator cannot log in

Confirm that:

- bootstrap email and password are configured for the active runtime mode
- the password satisfies the Identity policy
- the application database is reachable
- the account is active
- startup completed role and account initialization

Changing the configured password does not reset an existing account.

### Protected request returns 401

Confirm that:

- login succeeded
- the bearer header contains the returned token
- the token has not expired
- the account remains active
- the account role or status has not changed since token issuance
- the API uses the same issuer, audience, and signing key that issued the token

Log in again after any role or status change.

### Protected request returns 403

The token is valid, but the assigned role does not satisfy the endpoint policy. Review the account role through an Administrator session.

### Reorder event remains Pending

Investigate in order:

1. API publication log
2. Service Bus Emulator health
3. Processor receipt log
4. supplier-client submission log
5. supplier response or failure log
6. Processor persistence and settlement log

A retryable supplier or database failure intentionally leaves the event `Pending` until a later delivery succeeds or the delivery limit is exhausted.

### Supplier is alive but unhealthy

Use `/alive` and `/health` separately. The supplier process may be running while `supplierdb` is unavailable or its migration/configuration failed.

### Detailed references

- [API Reference](api-reference.md)
- [System Architecture](architecture.md)
- [Mock Supplier Service](mock-supplier-service.md)
- [Observability Runbook](observability-runbook.md)
- [Failure Scenarios](failure-scenarios.md)
- [Inventory Operations Case Study](inventory-operations-case-study.md)
