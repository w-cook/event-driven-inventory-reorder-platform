# Inventory Operations and Reliability Expansion — Case Study

## Context

The original Event-Driven Inventory Reorder Platform demonstrated a distributed backend workflow with an ASP.NET Core API, background worker, SQL-backed state, Docker-based local development, and queue-based reorder processing.

That foundation was useful, but practical operational systems also need authenticated user accounts, role-aware access, auditability, operator visibility, failure handling, and protection against duplicate message delivery.

## Expansion Goal

This expansion adds production-oriented concerns to the existing system without replacing the original event-driven architecture.

The goal is to show how a functional backend demo can evolve into a more operationally useful internal business system while keeping the implementation practical, locally reproducible, and accurately scoped.

## Key Engineering Themes

### Authenticated Operator Visibility

A React/TypeScript dashboard provides:

- login and logout
- signed-in identity and role visibility
- inventory items and low-stock conditions
- reorder state and processing history
- workflow summaries
- application and database health
- Administrator-only application-account management

The dashboard consumes protected API endpoints rather than duplicating business rules in the client. Inventory status calculation, reorder-event creation, account validation, and authorization remain backend responsibilities.

The current inventory dashboard is read-only. Operator and Administrator inventory mutation forms remain planned for a later UI phase, while the corresponding protected API endpoints can be exercised through the structured `.http` workflow.

### Persistent Identity and JWT Authentication

The earlier demo-header authentication scheme was replaced with persistent ASP.NET Core Identity accounts and signed JWT bearer tokens.

The implementation includes:

- securely hashed passwords through ASP.NET Core Identity
- a login endpoint that issues signed access tokens
- issuer, audience, signing-key, and expiration validation
- account-activity and security-stamp validation on authenticated requests
- no public registration endpoint
- optional bootstrap creation of the initial Administrator through local configuration
- Administrator-only creation and management of later accounts

Access tokens include the authenticated account identity, assigned roles, and the current Identity security stamp. Changing an account role or activation state updates that stamp, which immediately invalidates tokens issued under the previous account state.

The React client keeps the token only in memory. Page refresh or browser closure ends the frontend session. This avoids claiming refresh-token or durable browser-session infrastructure that the project does not implement.

### Role-Aware Workflows

The API uses policy-based authorization to enforce three operational roles:

- `Viewer` can read inventory, reorder workflow, and system-health data.
- `Operator` can perform the same read operations and can create or update inventory items.
- `Administrator` can perform operational actions, inspect the audit trail, and manage application accounts.

Role-aware rendering in the React client improves usability, but the API remains the security boundary. Viewer and Operator sessions do not call the Administrator-only accounts endpoint, and direct unauthorized requests are still rejected by backend policy enforcement.

### Administrator Account Management

The Administrator dashboard and API support:

- listing application accounts
- creating password-protected accounts
- assigning Viewer, Operator, or Administrator roles
- changing account roles
- deactivating and reactivating accounts
- preventing the final active Administrator from being demoted or deactivated

There is no public account-registration workflow. Account creation and lifecycle changes are intentionally controlled by authenticated Administrators.

### Audit Trail

Successful inventory creation and update operations create SQL-backed audit records.

Account creation, role changes, and activation changes are also audited.

Each audit record captures:

- the authenticated application user
- the active role
- the action performed
- the affected entity type and identifier
- the UTC occurrence time
- relevant action details

Inventory-update records include previous and current values, along with whether the operation created a new reorder event. Account-management records preserve the relevant previous and current role or activation state.

Rejected authorization, validation, and final-Administrator safeguard requests are not represented as completed business actions.

Audit records are exposed through an Administrator-only read endpoint:

```http
GET /api/audit-records
Authorization: Bearer <administrator-access-token>
```

### Reliable Message Processing

The Processor uses stable Service Bus message identifiers derived from reorder-event ids.

Successful messages are recorded in a SQL-backed `ProcessedMessages` ledger. Before performing business processing, the Processor checks whether the same message id and message type have already been handled. Duplicate deliveries are completed without repeating the business operation.

A unique database index provides additional protection against concurrent duplicate processing.

Valid messages that fail business processing create `FailedMessages` records containing the failure reason, original payload when available, delivery attempt count, and UTC failure time.

Retryable failures are abandoned until the configured maximum delivery count is reached. Messages that continue failing are then moved to the dead-letter queue. Malformed or unsupported payloads are dead-lettered immediately because retrying them cannot make them valid.

This design accepts at-least-once queue delivery and makes duplicate delivery harmless through idempotent processing rather than claiming exactly-once delivery.

The project still models an internal reorder-request workflow. A real supplier integration would occur before a reorder event is marked `Processed`, and the stable message id could be passed to that external system as an idempotency key.

### Observability

The API and Processor use shared Aspire service defaults for structured OpenTelemetry logging, metrics, ASP.NET Core tracing, HTTP client tracing, and local OTLP export.

Each API request accepts or generates an `X-Correlation-Id`. The identifier is returned to the caller and propagated through the Service Bus message so API and Processor lifecycle logs can be searched using the same value. This correlation identifier is diagnostic and remains separate from the stable Service Bus message id used for idempotency.

The platform also propagates W3C trace context through Service Bus application properties. Custom `PublishReorderMessage` and `ProcessReorderMessage` activities represent the application-owned producer and consumer boundaries. Their attributes describe the queue, message, inventory item, reorder event, delivery attempt, processing outcome, and settlement result.

Authentication requests and authorization outcomes use normal HTTP telemetry. Sensitive passwords, signing keys, password hashes, and raw access tokens are intentionally excluded from logs and trace attributes.

This approach extends the project’s existing Aspire infrastructure instead of adding a separate telemetry stack. It provides locally reproducible distributed diagnostics without claiming production log retention, cloud monitoring, or alerting services.

Operational verification steps are kept separately in `docs/observability-runbook.md`.

### Production-Oriented Testing

The API integration tests use real Identity users and login-issued bearer tokens instead of bypassing authentication.

The current xUnit v3 tests verify that:

- valid credentials issue a signed JWT with the expected identity, issuer, audience, and roles
- invalid credentials are rejected
- protected endpoints require valid bearer authentication
- inactive accounts cannot authenticate
- role and activation changes invalidate previously issued tokens
- Viewer, Operator, and Administrator policies enforce the intended access boundaries
- Administrators can list, create, update, deactivate, and reactivate accounts
- duplicate account emails, unsupported roles, and weak passwords are rejected
- the final active Administrator cannot be demoted or deactivated
- inventory and account-management actions create audit records
- successful Processor handling updates the reorder event and records the processed message
- duplicate delivery does not create a duplicate business result
- failed processing creates a persisted failure record with payload and attempt information
- correlation middleware generates an identifier when one is absent and preserves a caller-supplied identifier when one is provided
- an isolated API-to-Processor workflow test exercises the cross-component business path

Direct Worker settlement tests are not currently included because the Worker depends on concrete Azure Service Bus transport types. Adding a separate transport abstraction solely for those tests would add more complexity than the current scope requires.

The official Azure Service Bus Emulator remains available for manual producer/consumer verification without requiring paid Azure infrastructure.

### Repeatable Manual Verification

The structured API request file was updated from demo headers to real login-issued JWTs.

The Aspire-oriented workflow:

1. logs in as the locally configured bootstrap Administrator
2. creates Viewer and Operator test accounts
3. logs in as those accounts
4. reuses named-response access tokens for role-specific requests
5. verifies authorization, inventory changes, audit records, reorder processing, and health behavior from top to bottom

Credential values are resolved through ASP.NET Core User Secrets and are not stored in the repository.

## Tradeoffs and Boundaries

The authentication implementation is suitable for demonstrating local application-managed identity, JWT validation, role authorization, account lifecycle management, and immediate token revocation through security-stamp checks.

It does not claim:

- third-party enterprise identity-provider integration
- single sign-on
- refresh-token rotation
- persistent browser sessions
- public self-service registration
- password-reset or email-verification delivery
- production cloud key management

These boundaries keep the implementation focused and defensible while leaving clear extension points for a production environment.

## Portfolio Value

This expansion demonstrates practical backend engineering concerns that transfer across stacks:

- ASP.NET Core Identity integration
- signed JWT bearer authentication
- role-based authorization
- controlled account lifecycle management
- immediate invalidation of stale authorization tokens
- SQL-backed auditing
- distributed workflow reliability
- idempotent message processing
- duplicate-delivery protection
- retry and dead-letter behavior
- operational diagnostics
- production-oriented automated testing
- authenticated frontend visibility for backend systems
- maintainable and accurately scoped documentation

The expansion strengthens the project as evidence of practical C#/.NET backend and business-application development while keeping its limitations and claims fully defensible.
