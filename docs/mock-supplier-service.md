# Mock Supplier Service

## Purpose

`InventoryReorderPlatform.SupplierMockApi` is an independently hosted ASP.NET Core service that simulates an external supplier-order boundary for local development and automated verification.

The service exists to provide a realistic HTTP dependency for the inventory reorder workflow without requiring a paid service, cloud account, or real purchasing integration.

The background Processor submits reorder requests to this service after consuming the corresponding Service Bus message. The supplier boundary remains independently owned even though it participates in the complete reorder workflow.

## Responsibilities

The mock supplier service:

* accepts supplier-order requests over HTTP
* validates the supplier request and required idempotency header
* accepts and logs the propagated workflow correlation identifier
* persists accepted orders in its own SQL Server database
* makes repeated delivery of the same request harmless
* detects conflicting reuse of an idempotency key
* simulates delayed responses, transient failures, and permanent rejection
* exposes health, liveness, and OpenAPI endpoints
* runs through both .NET Aspire and Docker Compose

The supplier service owns its HTTP contracts and persistence model. It does not reference the inventory API, inventory data project, Processor, or internal queue-message contracts.

## Supplier Order Endpoint

```http
POST /api/supplier-orders
```

### Required Header

```http
Idempotency-Key: reorder-event-<reorder-event-id>
X-Correlation-Id: <workflow-correlation-id>
```

The request must contain exactly one nonempty `Idempotency-Key` value no longer than 200 characters.

The Processor supplies `X-Correlation-Id` so the submission can be followed through API, queue, Processor, typed-client, and supplier logs. Direct development requests that omit the header use the supplier request’s local trace identifier for logging.

### Request Body

```json
{
  "reorderEventId": 1001,
  "inventoryItemId": 25,
  "sku": "WIDGET-100",
  "requestedQuantity": 40,
  "triggeredAtUtc": "2026-08-02T13:30:00Z"
}
```

| Field               | Meaning                                                       |
| ------------------- | ------------------------------------------------------------- |
| `reorderEventId`    | Inventory-platform reorder event that originated the request  |
| `inventoryItemId`   | Inventory item associated with the reorder                    |
| `sku`               | Supplier-facing stock-keeping identifier                      |
| `requestedQuantity` | Immutable quantity requested by the originating reorder event |
| `triggeredAtUtc`    | UTC time at which the inventory reorder workflow began        |

The SKU is required and limited to 50 characters. `requestedQuantity` must be positive. `triggeredAtUtc` must contain a nondefault UTC value.

### Accepted Response

```json
{
  "supplierOrderId": "8d7c6d20-91be-4d08-a104-b831a2295b84",
  "idempotencyKey": "reorder-event-1001",
  "reorderEventId": 1001,
  "inventoryItemId": 25,
  "sku": "WIDGET-100",
  "requestedQuantity": 40,
  "triggeredAtUtc": "2026-08-02T13:30:00Z",
  "status": "Accepted",
  "acceptedAtUtc": "2026-08-02T13:31:00Z"
}
```

## Processor Integration

The Processor constructs the supplier request from the immutable `ReorderRequestedMessage` snapshot rather than rereading mutable inventory configuration.

It submits:

- reorder-event identifier
- inventory-item identifier
- SKU
- requested quantity
- UTC trigger time

The Service Bus message identifier is used as the supplier `Idempotency-Key`. For normal application workflows it follows this format:

```text
reorder-event-<ReorderEventId>
```

The Processor interprets supplier responses as follows:

| Supplier response | Processor behavior |
| --- | --- |
| `201 Created` | Persist supplier acceptance and complete the message |
| `200 OK` | Persist the replayed original acceptance and complete the message |
| `422 Unprocessable Entity` | Persist terminal supplier rejection and complete the message |
| `503 Service Unavailable` | Record a failed attempt and abandon for redelivery |
| Other unexpected response | Treat as a technical failure eligible for retry and dead-letter handling |

The HTTP resilience configuration does not internally retry unsafe `POST` operations. Service Bus abandonment and redelivery own supplier-submission retries so each attempt remains visible in processing and failure records.

## Idempotency Behavior

The service uses the supplied idempotency key as the durable identity of a supplier submission.

### New Request

A valid new request is persisted and returns:

```http
201 Created
```

### Identical Replay

Repeating the same key with the same business payload returns the previously accepted order:

```http
200 OK
```

The response retains the original:

* `supplierOrderId`
* `acceptedAtUtc`
* submitted payload
* accepted status

No additional supplier-order row is created.

### Conflicting Replay

Reusing an existing key with a different payload returns:

```http
409 Conflict
```

The original accepted order remains unchanged.

A unique database index on `IdempotencyKey` provides database-level protection in addition to the controller lookup. The controller also handles a concurrent unique-index collision by reloading the persisted order and applying the same identical-replay or conflicting-replay rules.

## Response Statuses

| Status                     | Meaning                                                         |
| -------------------------- | --------------------------------------------------------------- |
| `201 Created`              | A new supplier order was accepted                               |
| `200 OK`                   | An identical previously accepted order was replayed             |
| `400 Bad Request`          | The header or request failed validation                         |
| `409 Conflict`             | An existing idempotency key was reused with a different payload |
| `422 Unprocessable Entity` | The configured mock behavior permanently rejected the order     |
| `503 Service Unavailable`  | The configured mock behavior produced a transient failure       |

Transient `503` responses include:

```http
Retry-After: 1
```

## Configurable Behavior Modes

Configuration is read from the `SupplierBehavior` section.

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

Docker/local mode can override the same settings through environment-variable names:

```dotenv
SupplierBehavior__Mode=Normal
SupplierBehavior__DelayMilliseconds=1500
SupplierBehavior__TransientFailuresBeforeSuccess=2
SupplierBehavior__PermanentRejectionMessage=The supplier rejected the requested order.
```

The committed Docker configuration uses `Normal`. Failure modes are development and verification controls rather than production defaults.

### Normal

The request proceeds directly to acceptance.

### Delayed

The service waits for `DelayMilliseconds` before processing the request.

The configured delay must be between 0 and 30,000 milliseconds.

The shared HTTP resilience pipeline has a shorter per-attempt timeout than the mock service’s maximum configurable delay. A moderate delay such as five seconds demonstrates slow but successful processing. A delay beyond the HTTP attempt timeout is canceled and treated as a retryable technical failure.

### TransientFailure

The service returns `503 Service Unavailable` for the configured number of attempts associated with an idempotency key. A later attempt proceeds normally.

For example, with two configured transient failures:

```text
Attempt 1 → 503
Attempt 2 → 503
Attempt 3 → 201
Attempt 4 → 200
```

Transient attempt counters are held in process memory and are intended only for local failure simulation. Restarting the supplier process resets counters for requests that have not yet been accepted. Accepted orders remain durable in SQL Server and continue to replay safely after restart.

### PermanentRejection

The service returns `422 Unprocessable Entity` with the configured rejection message.

Transient failures and permanent rejections do not create supplier-order records.

## Persistence Boundary

The supplier service uses:

```text
SupplierDbContext
```

Its accepted orders are stored separately from the inventory platform’s Identity, inventory, reorder, audit, and message-processing tables.

Both local runtime modes may use the same SQL Server resource, but the supplier service connects to an independent database with its own:

* EF Core context
* models
* migration history
* tables
* unique constraints
* connection string

In Docker/local mode, the database is:

```text
InventoryReorderPlatformSupplierDb
```

This preserves service ownership without requiring another SQL Server container.

## Health and OpenAPI

The service exposes:

```http
GET /health
GET /alive
GET /openapi/v1.json
```

`/health` reports application and dependency health. `/alive` reports whether the process is running. The OpenAPI document describes the supplier HTTP surface for development and integration work.

The generated OpenAPI document should be inspected before it is treated as complete contract documentation. The required idempotency header is read explicitly from the incoming request, and Phase 12 verifies whether that header and every response status are represented in generated metadata, adding explicit OpenAPI annotations and examples where needed.

## Local Runtime

### Aspire

The supplier service and supplier database are orchestrated by `InventoryReorderPlatform.AppHost`.

Aspire provides dynamically assigned service endpoints together with service discovery, resource health, logs, metrics, and traces. The Processor resolves the supplier through the Aspire resource name `supplier`.

### Docker Compose

The supplier service is exposed to the host at:

```text
http://localhost:8082
```

Useful requests include:

```http
GET http://localhost:8082/health
GET http://localhost:8082/alive
GET http://localhost:8082/openapi/v1.json
```

The Processor reaches the supplier inside the Compose network at `http://supplier:8080`.

The complete local backend stack can be started with:

```bash
docker compose -f docker-compose.local.yml up -d --build
```

## Automated Verification

`InventoryReorderPlatform.SupplierMockApi.Tests` verifies:

* valid order acceptance
* persistence of accepted orders
* identical idempotent replay
* conflicting idempotency-key reuse
* missing-header validation
* invalid-quantity validation
* database-level uniqueness
* delayed responses
* transient failure and recovery
* permanent rejection
* replay of an accepted order after the mock mode changes

`InventoryReorderPlatform.Processor.Tests` additionally verifies:

* accepted `201 Created` and replayed `200 OK` responses
* required idempotency and correlation headers
* delayed client completion
* supplier response validation
* transient failure followed by successful redelivery
* permanent rejection persistence
* supplier acceptance followed by an initial local-save failure
* redelivery completing the local transaction without creating a second supplier order

The SQLite supplier integration-test database uses a relational unique constraint so tests exercise behavior that EF Core’s non-relational InMemory provider would not enforce.

## Intentional Limitations

The mock supplier service is not presented as a real supplier or purchasing integration.

It currently does not provide:

* authentication between the Processor and supplier
* real purchasing or supplier credentials
* physical inventory fulfillment
* shipment or delivery tracking
* automatic updates to `QuantityOnHand`
* production hosting
* durable transient-attempt counters

A supplier order being accepted remains distinct from stock being physically received. Inventory stock changes only through a later inventory update.

