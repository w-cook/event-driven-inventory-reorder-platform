# Event-Driven Inventory Reorder Platform

A distributed .NET portfolio project that models an internal inventory reorder workflow using an ASP.NET Core API, a background Processor, SQL Server, Azure-compatible queue messaging, role-aware operations, and a React/TypeScript dashboard.

## Project Overview

Inventory items have stock levels and reorder thresholds. When an item transitions into a low-stock state, the API:

1. changes the inventory item to `ReorderPending`
2. creates a `Pending` reorder event
3. records the successful inventory action in the audit trail
4. publishes a `ReorderRequestedMessage`

A separate Processor consumes the message, performs the internal reorder-request workflow, records the message as successfully processed, and changes the reorder event to `Processed`.

A processed reorder event does **not** mean that replacement stock has arrived. The Processor does not increase `QuantityOnHand`, and the inventory item remains `ReorderPending` while its quantity is at or below the reorder threshold. A later inventory update can represent receipt of stock and return the item to `Active`.

The project intentionally models a small internal business platform. It demonstrates reliable workflow patterns without claiming a production identity provider, real supplier integration, automated purchasing, or physical inventory fulfillment.

## What This Project Demonstrates

- ASP.NET Core Web API design
- background processing with a .NET Worker Service
- event-driven producer/consumer architecture
- reliable and idempotent message processing
- Entity Framework Core with SQL Server
- policy-based authorization and role-aware operations
- SQL-backed audit and processing records
- React/TypeScript operational visibility
- .NET Aspire distributed application orchestration
- Docker-based local infrastructure
- Azure-compatible messaging through the Azure Service Bus Emulator
- production-oriented design decisions without paid cloud infrastructure

## Tech Stack

- C#
- .NET 10
- ASP.NET Core Web API
- .NET Worker Service
- .NET Aspire
- Entity Framework Core
- SQL Server
- Azure Service Bus client libraries
- Azure Service Bus Emulator
- Docker and Docker Compose
- React
- TypeScript
- Vite
- xUnit v3

## Solution Structure

### `InventoryReorderPlatform.AppHost`

Orchestrates the API, Processor, and application SQL Server during Aspire development. The Service Bus Emulator and its SQL dependency are started separately through Docker Compose.

### `InventoryReorderPlatform.ServiceDefaults`

Provides shared Aspire service defaults for the distributed application.

### `InventoryReorderPlatform.Api`

Provides inventory management, reorder visibility, system health, demo authentication, authorization policies, audit-record access, and reorder-message publishing.

### `InventoryReorderPlatform.Processor`

Consumes reorder messages, applies duplicate protection, records successful and failed processing attempts, updates reorder-event state, retries transient failures, and dead-letters messages that cannot be processed successfully.

### `InventoryReorderPlatform.Data`

Contains the shared EF Core data layer:

- entity models
- `AppDbContext`
- schema migrations
- processed-message and failed-message persistence

### `InventoryReorderPlatform.Contracts`

Contains shared messaging contracts and configuration models used across projects.

### `InventoryReorderPlatform.Processor.Tests`

Contains reliability-focused processor tests covering successful processing, duplicate delivery, and failed-processing persistence.

### `client`

Contains the React/TypeScript operations dashboard for inventory visibility, low-stock filtering, reorder workflow history, and system-health status.

## Current Features

### Inventory Operations

- list and retrieve inventory items
- create and update inventory items
- server-controlled DTO and validation flow
- automatic status calculation:
  - `QuantityOnHand > ReorderThreshold` → `Active`
  - `QuantityOnHand <= ReorderThreshold` → `ReorderPending`
- reorder-event creation when an item transitions into `ReorderPending`
- duplicate reorder-event avoidance while an item remains in the same low-stock state
- reorder-history entries when inventory status changes
- separate inventory and reorder-event state

### Authorization and Audit Trail

- local demo authentication through the `X-Demo-User` header
- policy-based API authorization
- three access levels:
  - `Viewer` — read-only operational access
  - `Operator` — inventory creation and updates
  - `Administrator` — Operator access plus audit-record review
- SQL-backed audit records for successful inventory creation and updates
- update records containing previous values, current values, and whether a reorder event was created
- Administrator-only audit-record endpoint
- rejected authorization and validation requests do not create successful business-action audit records

### Operations Dashboard and Health

- React/TypeScript inventory operations dashboard
- inventory list and summary metrics
- low-stock filtering
- reorder workflow and processing-history view
- workflow summary metrics
- system-health panel with manual refresh
- independent loading and error states
- visible Operator demo-role indicator
- protected operations health endpoint reporting:
  - API status
  - database connectivity
  - inventory-item count
  - reorder-event count
  - UTC check time

### Reliable Message Processing

- queue-based reorder-message publishing and consumption
- stable Service Bus `MessageId` values derived from reorder-event ids
- SQL-backed `ProcessedMessages` idempotency ledger
- unique index on message id and message type
- duplicate-message detection before business processing
- duplicate deliveries completed without repeating the business operation
- successful reorder-event update and processed-message record saved together
- configurable maximum delivery attempts
- failed messages abandoned for retry before the configured threshold
- messages dead-lettered after the maximum delivery count
- malformed or unsupported payloads dead-lettered immediately
- SQL-backed `FailedMessages` records containing:
  - message id
  - message type
  - failure reason
  - original payload when available
  - delivery attempt count
  - UTC failure timestamp

## Core Workflow

1. An authenticated Operator or Administrator creates or updates an inventory item.
2. The API validates the request and applies the authorization policy.
3. When quantity becomes less than or equal to the reorder threshold, the item transitions to `ReorderPending`.
4. The API creates a reorder event with `Status = "Pending"`.
5. The API writes an audit record for the successful inventory action.
6. The API publishes a `ReorderRequestedMessage` with a stable message id.
7. The Processor checks whether that message id has already been processed.
8. For a new valid message, the Processor handles the reorder request, marks the event `Processed`, and writes a `ProcessedMessage` record.
9. A repeated delivery is recognized and completed without repeating the business operation.
10. A retryable failure is recorded and the queue message is abandoned until the configured delivery threshold is reached.
11. A repeatedly failing message is moved to the dead-letter queue.
12. The inventory item remains `ReorderPending` until a later inventory update raises its quantity above the threshold.

This keeps three related states distinct:

- **Inventory state:** whether current stock is healthy or requires reordering
- **Reorder-event state:** whether the background request is pending or processed
- **Message-processing state:** whether a queue message succeeded, was recognized as a duplicate, or failed
- **Audit state:** which authenticated demo user performed a successful inventory action

## Reliability Design

### Idempotency

The API publishes a stable message id in this format:

```text
reorder-event-<ReorderEventId>
```

Before processing a message, the Processor checks the `ProcessedMessages` ledger for the same message id and message type. If a matching record already exists, the delivery is treated as a duplicate and completed without repeating the business operation.

The database also enforces a unique composite index on message id and message type. This provides protection when duplicate deliveries are processed concurrently.

### Failure and Retry Behavior

Valid reorder messages that fail business processing create `FailedMessages` records for troubleshooting. Each failed attempt may create its own record so delivery history remains visible.

Before `MaxDeliveryAttempts` is reached, the Worker abandons the message so Service Bus can deliver it again. At the configured threshold, the Worker moves it to the dead-letter queue.

Malformed or unsupported payloads are dead-lettered immediately because they cannot be converted into a valid reorder-processing request.

### External Integration Boundary

The current Processor models successful handling of an internal reorder request. It does not submit a real supplier order.

In a production system, the external purchasing or supplier API call would occur before the reorder event is marked `Processed`. The stable message id could also be supplied to that external system as an idempotency key to prevent duplicate supplier orders during retries.

## API Access

Requests identify a local demo user with:

```http
X-Demo-User: viewer
```

| Header value | Role | Access |
| --- | --- | --- |
| `viewer` | Viewer | Read inventory, reorder workflow, and system health |
| `operator` | Operator | Viewer access plus inventory creation and updates |
| `admin` | Administrator | Operator access plus audit-record review |

Key protected endpoints include:

```http
GET /api/inventoryitems
X-Demo-User: viewer
```

```http
GET /api/reorderevents
X-Demo-User: viewer
```

```http
GET /api/operations/health
X-Demo-User: viewer
```

```http
GET /api/audit-records
X-Demo-User: admin
```

The demo authentication mechanism exists to exercise ASP.NET Core claims, roles, and authorization policies locally. It is not presented as a production identity-management solution.

## Data Model

### `InventoryItem`

- `Id`
- `Name`
- `Sku`
- `QuantityOnHand`
- `ReorderThreshold`
- `Status`
- `CreatedAt`
- `UpdatedAt`

### `ReorderEvent`

- `Id`
- `InventoryItemId`
- `QuantityAtTrigger`
- `TriggeredAt`
- `Status`

### `ReorderHistory`

- `Id`
- `InventoryItemId`
- `OldStatus`
- `NewStatus`
- `ChangedAt`

### `AuditRecord`

- `Id`
- `UserName`
- `Role`
- `Action`
- `EntityType`
- `EntityId`
- `Details`
- `OccurredAt`

### `ProcessedMessage`

- `Id`
- `MessageId`
- `MessageType`
- `ProcessedAtUtc`

### `FailedMessage`

- `Id`
- `MessageId`
- `MessageType`
- `Reason`
- `Payload`
- `AttemptCount`
- `FailedAtUtc`

## Testing

Run the backend test suite from the repository root:

```bash
dotnet test
```

The processor reliability tests verify that:

- successful processing changes the reorder event to `Processed`
- successful processing creates a `ProcessedMessage` ledger entry
- processing the same stable message id twice does not create a duplicate business result
- a message referencing a missing reorder event returns a failed outcome
- failed processing creates a `FailedMessage` record with its payload and attempt count

Run the frontend build check with:

```bash
cd client
npm run build
cd ..
```

Direct Worker settlement tests are not currently included. The Worker depends on concrete Azure Service Bus transport types, and adding a separate transport abstraction solely for unit testing would add more complexity than this phase requires. Retry and dead-letter behavior is implemented in the Worker and can be exercised through the local Service Bus Emulator.

## Screenshots

![Docker local stack running](docs/images/docker-local-stack.png)
![Inventory items endpoint](docs/images/inventory-items-endpoint.png)
![Reorder events showing processed workflow](docs/images/reorder-events-processed.png)
![Processor logs showing queue workflow](docs/images/processor-logs.png)

## Running the Project Locally

The project supports two local run modes.

### Aspire Mode

Best for normal development and debugging.

Aspire runs the API, Processor, and application SQL Server. The Service Bus Emulator remains an external local dependency.

#### 1. Start the Service Bus Emulator

From the repository root:

```bash
docker compose -f docker-compose.local.yml up -d sb-emulator-sql servicebus-emulator
```

Confirm the containers are running:

```bash
docker compose -f docker-compose.local.yml ps
```

Follow the emulator startup logs:

```bash
docker compose -f docker-compose.local.yml logs -f servicebus-emulator
```

Wait for startup to complete before sending a request that can publish a reorder message. Press `Ctrl+C` to stop following the logs without stopping the containers.

#### 2. Start the Aspire application

Run `InventoryReorderPlatform.AppHost` from Visual Studio or use:

```bash
dotnet run --project InventoryReorderPlatform.AppHost
```

Use the Aspire dashboard to find the current API and resource endpoints.

The React dashboard is not yet configured for Aspire's dynamic API endpoint. API calls can still be tested through the existing `.http` requests by replacing the host variable with the current Aspire API endpoint.

#### 3. Shut down Aspire mode

Stop the AppHost, then stop the Service Bus containers:

```bash
docker compose -f docker-compose.local.yml stop servicebus-emulator sb-emulator-sql
```

To remove the stopped containers and Compose network:

```bash
docker compose -f docker-compose.local.yml down
```

Do not add `-v` unless a full local data reset is intended.

### Docker / Local Stack Mode

Best for demonstrating the complete local containerized system.

From the repository root:

```bash
docker compose -f docker-compose.local.yml build
docker compose -f docker-compose.local.yml up -d
docker compose -f docker-compose.local.yml ps
```

The stack includes:

- application SQL Server
- Service Bus Emulator
- Service Bus Emulator SQL dependency
- Service Bus readiness gating
- API
- Processor

The API is exposed at:

```text
http://localhost:8080
```

Useful commands:

```bash
docker compose -f docker-compose.local.yml logs -f api
docker compose -f docker-compose.local.yml logs -f processor
docker compose -f docker-compose.local.yml logs servicebus-emulator
docker compose -f docker-compose.local.yml down
```

The readiness check prevents the API and Processor from starting before the emulator can accept connections.

### Operations Dashboard

The React dashboard is currently configured for Docker/local-stack mode.

After starting the full Docker stack:

```bash
cd client
npm install
npm run dev
```

The Vite development server proxies `/api` requests to the Docker-hosted API at `http://localhost:8080`. The shared frontend API client sends the `operator` demo identity for protected requests.

## Local Reset

To remove local application data and rebuild the complete Docker stack:

```bash
docker compose -f docker-compose.local.yml down -v
docker compose -f docker-compose.local.yml up -d --build
```

The `-v` option removes attached Docker volumes and should be used only when a full reset is intended.

## Implementation Notes

- Application timestamps are controlled by the server and stored in UTC.
- Inventory status is derived from quantity and threshold rather than posted directly by the client.
- Reorder events are created only when an item transitions into `ReorderPending`.
- Reorder history is created whenever inventory status changes.
- A `Processed` reorder event means that the Processor handled the internal request; it does not mean stock was received.
- The API and Processor share the same SQL-backed business and message-processing state.
- Aspire mode uses hybrid local orchestration: Aspire runs application services and SQL Server, while Docker Compose runs the Service Bus Emulator and its SQL dependency.
- Messaging is developed and tested locally with the official Azure Service Bus Emulator.
- The published project intentionally uses zero-cost local infrastructure rather than paid Azure resources.
- The dashboard-oriented `/api/operations/health` endpoint is separate from the Aspire `/health` and `/alive` endpoints.
- Health record counts are nullable so a failed database query is not misrepresented as zero records.
- The System Health panel can fail independently without hiding inventory and reorder data.

## Expansion Roadmap

The current portfolio expansion has completed:

- Phase 1 — Operations Dashboard Foundation
- Phase 2 — Authorization and Audit Trail
- Phase 3 — Reliable Message Processing

Planned work includes:

- structured logging and correlation identifiers
- OpenTelemetry tracing where practical
- documented workflow-debugging examples
- broader authorization and end-to-end workflow tests
- architecture and operational documentation
- unified frontend API configuration for Aspire and Docker run modes

See:

- `docs/inventory-operations-roadmap.md`
- `docs/inventory-operations-case-study.md`

## Scope

### In Scope

- inventory tracking and reorder-threshold logic
- event-driven background processing
- reliable and idempotent message consumption
- retry and dead-letter behavior
- relational business, audit, and processing data
- role-aware API operations
- operator-facing dashboard visibility
- containerized local infrastructure
- Azure-compatible, zero-cost local messaging
- truthful operational documentation

### Out of Scope

- production identity-provider integration
- persistent user-account management
- real purchasing or supplier integrations
- automated physical inventory receipt
- production cloud hosting
- Kubernetes
- complex administrative configuration UI
- an oversized general-purpose inventory platform

## Portfolio Positioning

This project is a distributed .NET business application with:

- an ASP.NET Core API producer
- a background Worker consumer
- SQL Server persistence
- reliable queue-based processing
- role-aware operations and auditing
- an operator-facing React dashboard
- Aspire and Docker local orchestration
- Azure-compatible messaging
- automated reliability tests

It demonstrates practical backend and distributed-system concerns while keeping its claims conservative and fully reproducible through local, zero-cost infrastructure.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
