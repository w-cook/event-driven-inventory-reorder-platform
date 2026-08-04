# Event-Driven Inventory Reorder Platform API Reference

## Purpose

This document describes the public HTTP surface of the Event-Driven Inventory Reorder Platform and its independently hosted mock supplier API.

It covers:

- authentication and role-based authorization
- inventory operations
- reorder workflow visibility
- audit and account administration
- operations health
- mock supplier submission
- request and response models
- validation rules
- expected HTTP status codes
- correlation identifiers, trace propagation, and supplier idempotency

This reference reflects the controller, DTO, middleware, security-policy, and health-endpoint implementations on the Phase 12 starting baseline.

## Service Addresses

Local addresses depend on the selected runtime mode.

### Docker/local mode

| Service | Base address |
| --- | --- |
| Inventory API | `http://localhost:8080` |
| Mock supplier API | `http://localhost:8082` |
| React client | Vite development address, normally `http://localhost:5173` |

Inside the Docker Compose network, the Processor reaches the supplier at:

```text
http://supplier:8080
```

### .NET Aspire mode

Aspire assigns service endpoints dynamically. Use the Aspire dashboard to open the `api`, `supplier`, and `client` resources.

The Processor resolves the supplier through the Aspire service-discovery resource name:

```text
supplier
```

## Common Conventions

### JSON

Controller request and response bodies use JSON.

```http
Content-Type: application/json
Accept: application/json
```

ASP.NET Core's default JSON naming policy produces camel-case property names.

### Date and time values

Dates and times are serialized as ISO 8601 values. Fields whose names end in `Utc` must represent UTC values.

Example:

```json
"triggeredAtUtc": "2026-08-04T12:30:00Z"
```

### Problem responses

Most explicit API errors use ASP.NET Core `ProblemDetails` or `ValidationProblemDetails`.

Typical problem response:

```json
{
  "type": "about:blank",
  "title": "Account not found.",
  "status": 404,
  "detail": "Account 'example-id' does not exist."
}
```

Typical validation response:

```json
{
  "type": "about:blank",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "reorderQuantity": [
      "The field ReorderQuantity must be between 1 and 2147483647."
    ]
  }
}
```

The exact `type`, trace identifier, and validation wording may vary with ASP.NET Core framework behavior.

### Inventory API correlation identifier

Every request to the inventory API accepts an optional header:

```http
X-Correlation-Id: caller-supplied-value
```

The inventory API:

1. trims and preserves the first nonempty caller-supplied value, or
2. generates a 32-character GUID value without hyphens when no usable value is supplied
3. adds the resolved identifier to the response as `X-Correlation-Id`
4. includes it in structured logging scopes
5. adds it to the active trace as `correlation.id`
6. propagates it through the reorder message and supplier request when a reorder is triggered

This header is diagnostic. It is separate from the stable Service Bus message identifier and supplier idempotency key.

### Authentication

Except for login and the unauthenticated mock supplier boundary, protected inventory-platform endpoints require a JWT bearer token:

```http
Authorization: Bearer <access-token>
```

A missing, expired, malformed, deactivated, or security-stamp-invalidated token returns:

```http
401 Unauthorized
```

An authenticated account that lacks the required role returns:

```http
403 Forbidden
```

### Application roles

| Role | Capabilities |
| --- | --- |
| `Viewer` | Read inventory, reorder workflow, and operations-health data |
| `Operator` | Viewer access plus inventory creation and updates |
| `Administrator` | Operator access plus audit review and account administration |

Role input is trimmed and matched case-insensitively, but API responses use the canonical values shown above.

There is no anonymous registration endpoint.

## Endpoint Summary

### Inventory Platform API

| Method | Route | Required access |
| --- | --- | --- |
| `POST` | `/api/auth/login` | Anonymous |
| `GET` | `/api/accounts` | Administrator |
| `POST` | `/api/accounts` | Administrator |
| `PATCH` | `/api/accounts/{id}/role` | Administrator |
| `PATCH` | `/api/accounts/{id}/status` | Administrator |
| `GET` | `/api/inventoryitems` | Viewer, Operator, or Administrator |
| `GET` | `/api/inventoryitems/{id}` | Viewer, Operator, or Administrator |
| `POST` | `/api/inventoryitems` | Operator or Administrator |
| `PUT` | `/api/inventoryitems/{id}` | Operator or Administrator |
| `GET` | `/api/reorderevents` | Viewer, Operator, or Administrator |
| `GET` | `/api/audit-records` | Administrator |
| `GET` | `/api/operations/health` | Viewer, Operator, or Administrator |
| `GET` | `/health` | Development environment |
| `GET` | `/alive` | Development environment |

### Mock Supplier API

| Method | Route | Required access |
| --- | --- | --- |
| `POST` | `/api/supplier-orders` | No service authentication |
| `GET` | `/health` | Development environment |
| `GET` | `/alive` | Development environment |
| `GET` | `/openapi/v1.json` | Development environment |

---

# Inventory Platform API

## Authentication

### Log in

```http
POST /api/auth/login
```

Authenticates an active local application account and returns a signed JWT access token.

The endpoint intentionally returns the same invalid-credential response for an unknown email, incorrect password, inactive account, or locked account.

#### Authorization

Anonymous.

#### Request model: `LoginRequest`

| Field | Type | Required | Validation |
| --- | --- | --- | --- |
| `email` | string | Yes | Valid email-address format |
| `password` | string | Yes | Nonempty |

#### Example request

```http
POST /api/auth/login
Content-Type: application/json
X-Correlation-Id: login-example-001

{
  "email": "admin@example.local",
  "password": "<local-password>"
}
```

#### Response model: `LoginResponse`

| Field | Type | Nullable | Meaning |
| --- | --- | --- | --- |
| `accessToken` | string | No | Signed JWT bearer token |
| `expiresAtUtc` | date-time | No | UTC token-expiration time |
| `userId` | string | No | ASP.NET Core Identity user identifier |
| `email` | string | No | Authenticated account email |
| `roles` | string array | No | Assigned application roles |

#### Example success response

```json
{
  "accessToken": "<jwt-access-token>",
  "expiresAtUtc": "2026-08-04T13:00:00Z",
  "userId": "2a8f0348-ec74-4ca3-b5c1-6ad37df17bb5",
  "email": "admin@example.local",
  "roles": [
    "Administrator"
  ]
}
```

#### Expected statuses

| Status | Meaning |
| --- | --- |
| `200 OK` | Credentials are valid and a token was issued |
| `400 Bad Request` | Request-model validation failed |
| `401 Unauthorized` | Email/password is invalid, the account is inactive, or the account is locked |

Example `401` body:

```json
{
  "title": "Login failed.",
  "status": 401,
  "detail": "Invalid email or password."
}
```

### JWT lifecycle behavior

JWT validation checks:

- issuer
- audience
- signing key and signature
- expiration
- account existence
- active account status
- current ASP.NET Core Identity security stamp

Changing an account's role or active status updates its security stamp. Previously issued tokens for that account then fail validation with `401 Unauthorized`.

The default access-token duration is configured as 30 minutes unless overridden.

---

## Account Administration

All account endpoints require the `Administrator` role.

### Account password policy

Accounts created through the API use the configured ASP.NET Core Identity password policy:

- minimum length: 10 characters
- at least one digit
- at least one lowercase character
- at least one uppercase character
- at least one non-alphanumeric character

Emails must be unique.

New accounts have lockout enabled. Five failed password attempts trigger a 15-minute lockout under the default configuration.

### Account response model: `AccountResponse`

| Field | Type | Nullable | Meaning |
| --- | --- | --- | --- |
| `id` | string | No | ASP.NET Core Identity user identifier |
| `email` | string | No | Account email |
| `roles` | string array | No | Assigned application roles |
| `isActive` | boolean | No | Whether the account can authenticate and use tokens |
| `createdAtUtc` | date-time | No | UTC account-creation time |

Example:

```json
{
  "id": "2a8f0348-ec74-4ca3-b5c1-6ad37df17bb5",
  "email": "viewer@example.local",
  "roles": [
    "Viewer"
  ],
  "isActive": true,
  "createdAtUtc": "2026-08-04T12:00:00Z"
}
```

### List accounts

```http
GET /api/accounts
```

Returns all application accounts ordered by email and then identifier.

#### Expected statuses

| Status | Meaning |
| --- | --- |
| `200 OK` | Accounts returned |
| `401 Unauthorized` | Missing or invalid bearer token |
| `403 Forbidden` | Authenticated account is not an Administrator |

#### Example response

```json
[
  {
    "id": "2a8f0348-ec74-4ca3-b5c1-6ad37df17bb5",
    "email": "admin@example.local",
    "roles": [
      "Administrator"
    ],
    "isActive": true,
    "createdAtUtc": "2026-08-04T11:00:00Z"
  },
  {
    "id": "92bf7e23-273c-46b2-9cf8-f8ad1c31cb5d",
    "email": "viewer@example.local",
    "roles": [
      "Viewer"
    ],
    "isActive": true,
    "createdAtUtc": "2026-08-04T12:00:00Z"
  }
]
```

### Create account

```http
POST /api/accounts
```

Creates an active password-protected application account and assigns one supported role.

#### Request model: `CreateAccountRequest`

| Field | Type | Required | Validation |
| --- | --- | --- | --- |
| `email` | string | Yes | Valid email format and unique |
| `password` | string | Yes | Must satisfy the Identity password policy |
| `role` | string | Yes | `Viewer`, `Operator`, or `Administrator` |

#### Example request

```json
{
  "email": "operator@example.local",
  "password": "<strong-local-password>",
  "role": "Operator"
}
```

#### Expected statuses

| Status | Meaning |
| --- | --- |
| `201 Created` | Account created |
| `400 Bad Request` | Model, password, email, or role validation failed |
| `401 Unauthorized` | Missing or invalid bearer token |
| `403 Forbidden` | Authenticated account is not an Administrator |
| `409 Conflict` | An account already uses the email address |

A successful response includes an `AccountResponse`. The `Location` header uses `/api/accounts/{id}` as the created-resource identifier, although the current API does not expose a GET-by-id account endpoint.

Example conflict:

```json
{
  "title": "Account already exists.",
  "status": 409,
  "detail": "An account with that email address already exists."
}
```

### Change account role

```http
PATCH /api/accounts/{id}/role
```

Assigns one canonical application role and removes other currently assigned application roles.

#### Route parameter

| Parameter | Type | Meaning |
| --- | --- | --- |
| `id` | string | Target ASP.NET Core Identity user identifier |

#### Request model: `UpdateAccountRoleRequest`

| Field | Type | Required | Validation |
| --- | --- | --- | --- |
| `role` | string | Yes | `Viewer`, `Operator`, or `Administrator` |

#### Example request

```json
{
  "role": "Viewer"
}
```

If the requested role is already the account's only role, the endpoint returns the unchanged account.

A role change updates the target account's security stamp and invalidates its previously issued JWTs.

The final active Administrator cannot be assigned another role.

#### Expected statuses

| Status | Meaning |
| --- | --- |
| `200 OK` | Role returned unchanged or updated successfully |
| `400 Bad Request` | Unsupported role or Identity update failure |
| `401 Unauthorized` | Missing or invalid bearer token |
| `403 Forbidden` | Authenticated account is not an Administrator |
| `404 Not Found` | Target account does not exist |
| `409 Conflict` | The operation would remove the final active Administrator |

Example final-Administrator conflict:

```json
{
  "title": "Final Administrator protected.",
  "status": 409,
  "detail": "The final active Administrator cannot be assigned another role."
}
```

### Change account active status

```http
PATCH /api/accounts/{id}/status
```

Activates or deactivates an account.

#### Request model: `UpdateAccountStatusRequest`

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `isActive` | boolean | Yes in the JSON contract | Desired active status |

Example:

```json
{
  "isActive": false
}
```

If the account already has the requested status, the endpoint returns the unchanged account.

A status change updates the account's security stamp and invalidates previously issued JWTs.

The final active Administrator cannot be deactivated.

#### Expected statuses

| Status | Meaning |
| --- | --- |
| `200 OK` | Status returned unchanged or updated successfully |
| `400 Bad Request` | Identity security-stamp or account update failed |
| `401 Unauthorized` | Missing or invalid bearer token |
| `403 Forbidden` | Authenticated account is not an Administrator |
| `404 Not Found` | Target account does not exist |
| `409 Conflict` | The operation would deactivate the final active Administrator |

Example final-Administrator conflict:

```json
{
  "title": "Final Administrator protected.",
  "status": 409,
  "detail": "The final active Administrator cannot be deactivated."
}
```

---

## Inventory Items

### Inventory model semantics

| Field | Meaning |
| --- | --- |
| `quantityOnHand` | Current recorded physical stock |
| `reorderThreshold` | Stock level at or below which a reorder is required |
| `reorderQuantity` | Configured amount copied into a future reorder request |
| `status` | Current inventory state: `Active` or `ReorderPending` |

When an item transitions from `Active` into a quantity at or below its threshold, the API:

1. changes the inventory item to `ReorderPending`
2. creates a `Pending` reorder event
3. copies `reorderQuantity` into the event's immutable `requestedQuantity`
4. writes a status-history record
5. writes an audit record for the successful inventory action
6. saves the database changes
7. publishes a correlated `ReorderRequestedMessage`

An item that remains below the threshold while already `ReorderPending` does not create another reorder event.

When stock rises above the threshold, the item returns to `Active`. A later transition back into low stock creates a new reorder event.

Supplier acceptance does not increase `quantityOnHand`.

### Inventory create/update request model

`CreateInventoryItemRequest` and `UpdateInventoryItemRequest` use the same fields and validation rules.

| Field | Type | Required | Validation |
| --- | --- | --- | --- |
| `name` | string | Yes | Maximum 50 characters |
| `sku` | string | Yes | Maximum 50 characters |
| `quantityOnHand` | integer | Yes | `0` through `2147483647` |
| `reorderThreshold` | integer | Yes | `0` through `2147483647` |
| `reorderQuantity` | integer | Yes | `1` through `2147483647` |

`name` and `sku` are trimmed before persistence.

Example:

```json
{
  "name": "Industrial Filter",
  "sku": "FILTER-100",
  "quantityOnHand": 3,
  "reorderThreshold": 5,
  "reorderQuantity": 20
}
```

### Inventory response model: `InventoryItemResponse`

| Field | Type | Nullable | Meaning |
| --- | --- | --- | --- |
| `id` | integer | No | Inventory-item identifier |
| `name` | string | No | Item name |
| `sku` | string | No | Stock-keeping identifier |
| `quantityOnHand` | integer | No | Current recorded stock |
| `reorderThreshold` | integer | No | Low-stock threshold |
| `reorderQuantity` | integer | No | Quantity configured for future reorder events |
| `status` | string | No | `Active` or `ReorderPending` |
| `createdAt` | date-time | No | Creation time |
| `updatedAt` | date-time | No | Most recent update time |

Example:

```json
{
  "id": 25,
  "name": "Industrial Filter",
  "sku": "FILTER-100",
  "quantityOnHand": 3,
  "reorderThreshold": 5,
  "reorderQuantity": 20,
  "status": "ReorderPending",
  "createdAt": "2026-08-04T12:30:00Z",
  "updatedAt": "2026-08-04T12:30:00Z"
}
```

### List inventory items

```http
GET /api/inventoryitems
```

#### Authorization

Viewer, Operator, or Administrator.

#### Behavior

Returns all inventory items ordered by descending creation time.

#### Expected statuses

| Status | Meaning |
| --- | --- |
| `200 OK` | Inventory items returned |
| `401 Unauthorized` | Missing or invalid bearer token |
| `403 Forbidden` | Authenticated account lacks an inventory-read role |

### Get inventory item

```http
GET /api/inventoryitems/{id}
```

#### Authorization

Viewer, Operator, or Administrator.

#### Route parameter

| Parameter | Type | Validation |
| --- | --- | --- |
| `id` | integer | Must match the integer route constraint |

#### Expected statuses

| Status | Meaning |
| --- | --- |
| `200 OK` | Inventory item returned |
| `401 Unauthorized` | Missing or invalid bearer token |
| `403 Forbidden` | Authenticated account lacks an inventory-read role |
| `404 Not Found` | Inventory item does not exist |

The current not-found result is a string body:

```json
"InventoryItemId '25' does not exist."
```

### Create inventory item

```http
POST /api/inventoryitems
```

#### Authorization

Operator or Administrator.

#### Behavior

Creates the item and, when initially low on stock, starts a reorder workflow.

#### Expected statuses

| Status | Meaning |
| --- | --- |
| `201 Created` | Inventory item created |
| `400 Bad Request` | Request-model validation failed |
| `401 Unauthorized` | Missing or invalid bearer token |
| `403 Forbidden` | Authenticated account lacks an inventory-operate role |
| `500 Internal Server Error` | An unhandled infrastructure error occurred, including a queue-publication failure after business state was saved |

The success response is an `InventoryItemResponse` and includes a `Location` header resolving to `GET /api/inventoryitems/{id}`.

A queue-publication failure is logged and rethrown. The database state may therefore contain a pending reorder event even when the HTTP request ultimately returns `500`. Recovery expectations are documented separately in `failure-scenarios.md`.

### Update inventory item

```http
PUT /api/inventoryitems/{id}
```

#### Authorization

Operator or Administrator.

#### Behavior

Replaces the mutable inventory fields and applies the stock-status transition rules.

#### Expected statuses

| Status | Meaning |
| --- | --- |
| `200 OK` | Inventory item updated |
| `400 Bad Request` | Request-model validation failed |
| `401 Unauthorized` | Missing or invalid bearer token |
| `403 Forbidden` | Authenticated account lacks an inventory-operate role |
| `404 Not Found` | Inventory item does not exist |
| `500 Internal Server Error` | An unhandled infrastructure error occurred, including a queue-publication failure after business state was saved |

The current not-found result is a string body:

```json
"InventoryItemId '25' does not exist."
```

---

## Reorder Workflow

### List reorder events

```http
GET /api/reorderevents
```

#### Authorization

Viewer, Operator, or Administrator.

#### Behavior

Returns all reorder events ordered by descending trigger time.

### Reorder response model: `ReorderEventResponse`

| Field | Type | Nullable | Meaning |
| --- | --- | --- | --- |
| `id` | integer | No | Reorder-event identifier |
| `inventoryItemId` | integer | No | Originating inventory item |
| `inventoryItem` | object | Yes | Optional navigation property; the current endpoint projection does not populate it |
| `quantityAtTrigger` | integer | No | Physical stock snapshot when the event began |
| `requestedQuantity` | integer | No | Immutable supplier-request quantity snapshot |
| `triggeredAt` | date-time | No | Time the reorder event began |
| `status` | string | No | Current workflow state |
| `supplierOrderId` | UUID | Yes | Supplier-owned order identifier after acceptance |
| `supplierOrderStatus` | string | Yes | Supplier status observed by the inventory platform |
| `supplierAcceptedAtUtc` | date-time | Yes | Supplier acceptance time |
| `supplierRejectionReason` | string | Yes | Permanent supplier-rejection reason |

Current reorder statuses:

| Status | Meaning |
| --- | --- |
| `Pending` | Awaiting a terminal supplier outcome or retry |
| `SupplierAccepted` | Supplier accepted the order request |
| `SupplierRejected` | Supplier permanently rejected the request |
| `Processed` | Legacy terminal state retained for workflows completed before supplier submission was introduced |

Example accepted event:

```json
{
  "id": 1001,
  "inventoryItemId": 25,
  "inventoryItem": null,
  "quantityAtTrigger": 3,
  "requestedQuantity": 20,
  "triggeredAt": "2026-08-04T12:30:00Z",
  "status": "SupplierAccepted",
  "supplierOrderId": "8d7c6d20-91be-4d08-a104-b831a2295b84",
  "supplierOrderStatus": "Accepted",
  "supplierAcceptedAtUtc": "2026-08-04T12:30:02Z",
  "supplierRejectionReason": null
}
```

Example rejected event:

```json
{
  "id": 1002,
  "inventoryItemId": 26,
  "inventoryItem": null,
  "quantityAtTrigger": 2,
  "requestedQuantity": 15,
  "triggeredAt": "2026-08-04T12:35:00Z",
  "status": "SupplierRejected",
  "supplierOrderId": null,
  "supplierOrderStatus": null,
  "supplierAcceptedAtUtc": null,
  "supplierRejectionReason": "The supplier rejected the requested order."
}
```

#### Expected statuses

| Status | Meaning |
| --- | --- |
| `200 OK` | Reorder events returned |
| `401 Unauthorized` | Missing or invalid bearer token |
| `403 Forbidden` | Authenticated account lacks an inventory-read role |

---

## Audit Records

### List audit records

```http
GET /api/audit-records
```

Returns successful inventory and account-management audit records ordered by descending occurrence time.

#### Authorization

Administrator.

### Response model: `AuditRecordResponse`

| Field | Type | Nullable | Meaning |
| --- | --- | --- | --- |
| `id` | integer | No | Audit-record identifier |
| `userName` | string | No | Acting account |
| `role` | string | No | Acting role |
| `action` | string | No | Audit action identifier |
| `entityType` | string | No | Affected entity type |
| `entityId` | string | No | Affected entity identifier |
| `details` | string | Yes | Serialized action-specific details |
| `occurredAt` | date-time | No | Action occurrence time |

Example:

```json
[
  {
    "id": 75,
    "userName": "admin@example.local",
    "role": "Administrator",
    "action": "InventoryItemUpdated",
    "entityType": "InventoryItem",
    "entityId": "25",
    "details": "{\"Previous\":{\"QuantityOnHand\":8},\"Current\":{\"QuantityOnHand\":3},\"ReorderEventCreated\":true}",
    "occurredAt": "2026-08-04T12:30:00Z"
  }
]
```

`details` is returned as a nullable string containing serialized action-specific data rather than as a nested JSON object.

#### Expected statuses

| Status | Meaning |
| --- | --- |
| `200 OK` | Audit records returned |
| `401 Unauthorized` | Missing or invalid bearer token |
| `403 Forbidden` | Authenticated account is not an Administrator |

---

## Operations Health

### Get operations health

```http
GET /api/operations/health
```

Provides dashboard-oriented application and database status.

This endpoint is separate from the Aspire-style `/health` and `/alive` infrastructure endpoints.

#### Authorization

Viewer, Operator, or Administrator.

### Response model: `OperationsHealthResponse`

| Field | Type | Nullable | Meaning |
| --- | --- | --- | --- |
| `status` | string | No | Overall operations status |
| `databaseStatus` | string | No | Database connectivity status |
| `inventoryItemCount` | integer | Yes | Current item count, or null when unavailable |
| `reorderEventCount` | integer | Yes | Current event count, or null when unavailable |
| `checkedAt` | date-time | No | Check time |

Healthy example:

```json
{
  "status": "Healthy",
  "databaseStatus": "Connected",
  "inventoryItemCount": 12,
  "reorderEventCount": 5,
  "checkedAt": "2026-08-04T12:40:00Z"
}
```

Unhealthy example:

```json
{
  "status": "Unhealthy",
  "databaseStatus": "Unavailable",
  "inventoryItemCount": null,
  "reorderEventCount": null,
  "checkedAt": "2026-08-04T12:40:00Z"
}
```

#### Expected statuses

| Status | Meaning |
| --- | --- |
| `200 OK` | Database connection and count queries succeeded |
| `401 Unauthorized` | Missing or invalid bearer token |
| `403 Forbidden` | Authenticated account lacks an inventory-read role |
| `503 Service Unavailable` | Database connection or query failed |

---

## Inventory API Infrastructure Endpoints

### Readiness health

```http
GET /health
```

Runs all registered health checks. It is mapped only when the service environment is `Development`.

Expected statuses:

- `200 OK` when all checks are healthy
- `503 Service Unavailable` when a registered readiness check is unhealthy

### Liveness health

```http
GET /alive
```

Runs only checks tagged `live`. It is mapped only when the service environment is `Development`.

Expected statuses:

- `200 OK` when the process is alive
- `503 Service Unavailable` if a tagged liveness check fails

### Generated OpenAPI

At the Phase 12 starting baseline, the inventory API does not yet register or map a generated OpenAPI document. Phase 12 adds and verifies that generated contract separately from this human-readable reference.

When added, the document must represent:

- JWT bearer authentication
- endpoint role requirements
- `X-Correlation-Id`
- request and response schemas
- validation and problem responses
- all expected statuses documented above

---

# Mock Supplier API

## Service boundary

`InventoryReorderPlatform.SupplierMockApi` is an independently hosted development service that models an external supplier-order boundary.

It owns:

- its request and response contracts
- supplier-order persistence
- its EF Core context and migrations
- durable idempotency behavior
- configurable delay, transient failure, and permanent rejection simulation
- health and OpenAPI endpoints

It intentionally does not use the inventory API's internal DTOs, data context, or Service Bus message contract.

The service currently does not require service-to-service authentication. It must not be described as a production supplier integration.

## Submit supplier order

```http
POST /api/supplier-orders
```

### Authorization

No service authentication.

### Required and optional headers

| Header | Required | Validation and meaning |
| --- | --- | --- |
| `Idempotency-Key` | Yes | Exactly one nonempty value, trimmed, maximum 200 characters |
| `X-Correlation-Id` | No | Exactly one useful value is logged when supplied; otherwise the supplier request trace identifier is used |

Example:

```http
Idempotency-Key: reorder-event-1001
X-Correlation-Id: inventory-workflow-1001
```

The Processor uses the stable Service Bus message identifier as the idempotency key. Normal application-generated values use:

```text
reorder-event-<ReorderEventId>
```

The supplier logs the resolved correlation identifier. The current supplier controller does not explicitly echo it as a response header.

W3C HTTP trace context is propagated automatically by the instrumented Processor `HttpClient`.

### Request model: `CreateSupplierOrderRequest`

| Field | Type | Required | Validation |
| --- | --- | --- | --- |
| `reorderEventId` | integer | Yes | `1` through `2147483647` |
| `inventoryItemId` | integer | Yes | `1` through `2147483647` |
| `sku` | string | Yes | 1–50 characters and not empty or whitespace after trimming |
| `requestedQuantity` | integer | Yes | `1` through `2147483647` |
| `triggeredAtUtc` | date-time | Yes | Nondefault value whose `DateTime.Kind` is UTC |

Example:

```json
{
  "reorderEventId": 1001,
  "inventoryItemId": 25,
  "sku": "FILTER-100",
  "requestedQuantity": 20,
  "triggeredAtUtc": "2026-08-04T12:30:00Z"
}
```

The supplier trims `sku` before persistence.

### Response model: `SupplierOrderResponse`

| Field | Type | Nullable | Meaning |
| --- | --- | --- | --- |
| `supplierOrderId` | UUID | No | Supplier-owned accepted-order identifier |
| `idempotencyKey` | string | No | Durable request identity |
| `reorderEventId` | integer | No | Originating reorder event |
| `inventoryItemId` | integer | No | Originating inventory item |
| `sku` | string | No | Normalized supplier-facing SKU |
| `requestedQuantity` | integer | No | Immutable requested quantity |
| `triggeredAtUtc` | date-time | No | Original reorder trigger time |
| `status` | string | No | Currently `Accepted` |
| `acceptedAtUtc` | date-time | No | Original supplier acceptance time |

Example:

```json
{
  "supplierOrderId": "8d7c6d20-91be-4d08-a104-b831a2295b84",
  "idempotencyKey": "reorder-event-1001",
  "reorderEventId": 1001,
  "inventoryItemId": 25,
  "sku": "FILTER-100",
  "requestedQuantity": 20,
  "triggeredAtUtc": "2026-08-04T12:30:00Z",
  "status": "Accepted",
  "acceptedAtUtc": "2026-08-04T12:30:02Z"
}
```

## Supplier idempotency outcomes

### New key and valid payload

The supplier persists one accepted order and returns:

```http
201 Created
```

### Existing key and identical business payload

The supplier returns the original accepted order and does not create another row:

```http
200 OK
```

The response retains the original:

- `supplierOrderId`
- `acceptedAtUtc`
- payload
- `Accepted` status

Payload equivalence requires equality of:

- `reorderEventId`
- `inventoryItemId`
- trimmed `sku`
- `requestedQuantity`
- `triggeredAtUtc`

### Existing key and different business payload

The supplier preserves the original order and returns:

```http
409 Conflict
```

Example:

```json
{
  "title": "Idempotency key conflict",
  "status": 409,
  "detail": "The supplied idempotency key has already been used for a different supplier-order payload."
}
```

### Concurrent submission protection

A unique database index protects `IdempotencyKey`.

If concurrent requests pass the initial application lookup, the controller handles the database uniqueness collision by reloading the accepted order and applying the same identical-replay or conflicting-replay rules.

### Existing accepted order takes precedence over simulation

The supplier checks for a durable accepted order before applying configured behavior simulation.

Therefore, an accepted submission continues to return `200 OK` even if the mock service is later changed to delayed, transient-failure, or permanent-rejection mode.

## Supplier behavior modes

Configuration is read from the `SupplierBehavior` section.

### `Normal`

A valid new order is accepted immediately.

### `Delayed`

The service waits for `DelayMilliseconds` before continuing.

Valid configured range:

```text
0–30000 milliseconds
```

A moderate delay can complete successfully. A delay longer than the Processor HTTP attempt timeout becomes a retryable technical failure from the Processor's perspective.

### `TransientFailure`

The service returns `503 Service Unavailable` for the configured number of attempts associated with an idempotency key. A later attempt proceeds normally.

Example with two configured failures:

```text
Attempt 1 -> 503
Attempt 2 -> 503
Attempt 3 -> 201
Attempt 4 -> 200
```

The response includes:

```http
Retry-After: 1
```

Transient-attempt counters are held in process memory. Restarting the supplier resets counters for keys that have not yet been accepted. Accepted orders remain durable.

### `PermanentRejection`

The service returns:

```http
422 Unprocessable Entity
```

The configured rejection message is returned through `ProblemDetails`.

No accepted supplier-order row is created.

## Supplier expected statuses

| Status | Meaning |
| --- | --- |
| `200 OK` | Identical replay of a previously accepted order |
| `201 Created` | New supplier order accepted |
| `400 Bad Request` | Header or request-body validation failed |
| `409 Conflict` | Idempotency key was reused with a different payload |
| `422 Unprocessable Entity` | Configured permanent supplier rejection |
| `503 Service Unavailable` | Configured transient supplier failure |
| `500 Internal Server Error` | Unhandled database or infrastructure failure |

Example missing-header validation:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Idempotency-Key": [
      "A single non-empty 'Idempotency-Key' header is required."
    ]
  }
}
```

## Processor interpretation of supplier responses

| Supplier response | Processor behavior |
| --- | --- |
| `201 Created` | Store acceptance details, mark the event `SupplierAccepted`, record the processed message, and complete the Service Bus message |
| `200 OK` | Treat the durable replay as acceptance and complete local processing |
| `422 Unprocessable Entity` | Store the rejection reason, mark the event `SupplierRejected`, record the terminal outcome, and complete the Service Bus message |
| `503 Service Unavailable` | Record a failed processing attempt and abandon the message for Service Bus redelivery |
| Other unexpected or invalid response | Treat as a technical failure eligible for retry and eventual dead-letter handling |

The shared HTTP resilience handler does not internally retry unsafe `POST` operations. Service Bus abandonment and redelivery own supplier-submission retries.

## Supplier infrastructure endpoints

### Readiness health

```http
GET /health
```

Mapped only in `Development`.

### Liveness health

```http
GET /alive
```

Mapped only in `Development`.

### Generated OpenAPI

```http
GET /openapi/v1.json
```

Mapped only in `Development`.

The generated supplier document must be verified against this reference. In particular, explicit OpenAPI metadata is needed to ensure that it represents:

- the required `Idempotency-Key` header
- the optional `X-Correlation-Id` header
- `200`, `201`, `400`, `409`, `422`, and `503` responses
- request validation
- supplier response and problem schemas

---

# Correlation and Trace Propagation

## Complete workflow

The intended distributed trace is:

```text
POST or PUT /api/inventoryitems
└── PublishReorderMessage
    └── ProcessReorderMessage
        └── POST /api/supplier-orders
```

## Correlation value flow

1. An inventory API request accepts or generates `X-Correlation-Id`.
2. The API returns that value to the caller.
3. The API includes it in structured logs and the active activity.
4. The API publishes it with the Service Bus message.
5. The Processor restores it into processing diagnostics.
6. The typed supplier client sends it as `X-Correlation-Id`.
7. The supplier includes it in its logging scope.

## Trace context flow

The application propagates W3C trace context through Service Bus application properties.

Custom activities represent application-owned queue boundaries:

- `PublishReorderMessage`
- `ProcessReorderMessage`

The outgoing supplier HTTP request is instrumented under the consumer activity.

The correlation identifier supplements trace identifiers for human filtering; it does not replace W3C trace context.

---

# Contract and Security Boundaries

The public API intentionally does not provide:

- anonymous account registration
- refresh-token rotation
- persistent browser sessions
- password-reset or email-verification workflows
- third-party identity-provider or single-sign-on integration
- service-to-service supplier authentication
- real purchasing credentials
- shipment or delivery tracking
- automatic receipt of supplier stock
- production hosting guarantees

The mock supplier service is a local development and verification boundary. Supplier acceptance means that the external order request was accepted; it does not mean that stock was delivered or added to `quantityOnHand`.

Secrets such as JWT signing keys and bootstrap Administrator credentials must be supplied outside source control.

---

# Phase 12 OpenAPI Verification Checklist

Use this checklist when inspecting the generated inventory and supplier OpenAPI documents.

## Inventory API

- all 12 business and operations endpoints are present
- login is marked anonymous
- protected endpoints use a bearer security scheme
- endpoint role requirements are described
- `X-Correlation-Id` is documented
- request models contain required, range, email, and length constraints
- response schemas match the DTOs
- `401` and `403` are represented on protected endpoints
- account `400`, `404`, and `409` outcomes are represented
- inventory `400`, `404`, and successful outcomes are represented
- operations health includes both `200` and `503`
- problem and validation schemas are usable
- examples contain no credentials or real secrets

## Supplier API

- `Idempotency-Key` is represented as a required header
- `X-Correlation-Id` is represented as an optional header
- request constraints match the source model and controller checks
- `200` and `201` use `SupplierOrderResponse`
- `400`, `409`, `422`, and `503` use problem responses
- idempotent replay behavior is described
- conflicting replay behavior is described
- generated examples contain no unsafe defaults or real supplier claims
