# Inventory Operations and Reliability Expansion — Case Study

## Context

The original Event-Driven Inventory Reorder Platform demonstrated a distributed backend workflow with an ASP.NET Core API, background worker, SQL-backed state, Docker-based local development, and queue-based reorder processing.

That foundation is useful, but practical operational systems also need visibility, authorization, auditability, failure handling, and protection against duplicate message delivery.

## Expansion Goal

This expansion adds production-readiness concerns to the existing system without replacing the original architecture.

The goal is to show how a functional event-driven demo can evolve into a more operationally useful internal business system while keeping the implementation practical, reproducible, and accurately scoped.

## Key Engineering Themes

### Operator Visibility

A React/TypeScript dashboard provides visibility into inventory items, low-stock conditions, reorder state, processing history, workflow summaries, and system health.

The dashboard consumes the existing API rather than duplicating business rules in the client. Inventory status calculation, reorder-event creation, and authorization remain backend responsibilities.

### Role-Aware Workflows

The API uses a local demo authentication scheme and policy-based authorization to model three operational roles:

- `Viewer` can read inventory, reorder workflow, and system-health data.
- `Operator` can perform the same read operations and can create or update inventory items.
- `Administrator` can perform operational actions and inspect the audit trail.

The React dashboard identifies itself as using the Operator demo role and sends the corresponding demo-user header through a shared frontend API client.

The authentication mechanism is intentionally local and portfolio-focused. It demonstrates ASP.NET Core authentication handlers, claims, roles, and authorization policies without claiming integration with a production identity provider.

### Audit Trail

Successful inventory creation and update operations create SQL-backed audit records.

Each audit record captures:

- the authenticated demo user
- the active role
- the action performed
- the affected entity type and identifier
- the UTC occurrence time
- relevant action details

Inventory-update records include previous and current values, along with whether the operation created a new reorder event. This makes important operator actions reviewable without treating rejected authorization or validation requests as completed business operations.

Audit records are exposed through an Administrator-only read endpoint:

```http
GET /api/audit-records
X-Demo-User: admin
```

### Reliable Message Processing

The Processor now uses stable Service Bus message identifiers derived from reorder-event ids.

Successful messages are recorded in a SQL-backed `ProcessedMessages` ledger. Before performing business processing, the Processor checks whether the same message id and message type have already been handled. Duplicate deliveries are completed without repeating the business operation.

A unique database index provides additional protection against concurrent duplicate processing.

Valid messages that fail business processing create `FailedMessages` records containing the failure reason, original payload when available, delivery attempt count, and UTC failure time.

Retryable failures are abandoned until the configured maximum delivery count is reached. Messages that continue failing are then moved to the dead-letter queue. Malformed or unsupported payloads are dead-lettered immediately because retrying them cannot make them valid.

This design accepts at-least-once queue delivery and makes duplicate delivery harmless through idempotent processing rather than claiming exactly-once delivery.

The project still models an internal reorder-request workflow. A real supplier integration would occur before a reorder event is marked `Processed`, and the stable message id could be passed to that external system as an idempotency key.

### Observability

The API and Processor use the shared Aspire service defaults for structured OpenTelemetry logging, metrics, ASP.NET Core tracing, HTTP client tracing, and local OTLP export.

Each API request accepts or generates an `X-Correlation-Id`. The identifier is returned to the caller and propagated through the Service Bus message so API and Processor lifecycle logs can be searched using the same value. This correlation identifier is diagnostic and remains separate from the stable Service Bus message id used for idempotency.

The platform also propagates W3C trace context through Service Bus application properties. Custom `PublishReorderMessage` and `ProcessReorderMessage` activities represent the application-owned producer and consumer boundaries. Their attributes describe the queue, message, inventory item, reorder event, delivery attempt, processing outcome, and settlement result.

This approach extends the project’s existing Aspire infrastructure instead of adding a separate telemetry stack. It provides locally reproducible distributed diagnostics without claiming production log retention, cloud monitoring, or alerting services.

Operational verification steps are kept separately in `docs/observability-runbook.md`.

### Production-Oriented Testing

The Processor business logic is separated from Service Bus transport settlement so its reliability behavior can be tested directly.

The current xUnit v3 tests verify that:

- successful processing updates the reorder event and records the processed message
- duplicate delivery does not create a duplicate business result
- failed processing creates a persisted failure record with payload and attempt information
- correlation middleware generates an identifier when one is absent and preserves a caller-supplied identifier when one is provided

Direct Worker settlement tests are not currently included because the Worker depends on concrete Azure Service Bus transport types. Adding a separate transport abstraction solely for those tests would add more complexity than this phase requires.

The official Azure Service Bus Emulator remains available for manual producer/consumer verification without requiring paid Azure infrastructure.

## Portfolio Value

This project phase demonstrates practical backend engineering concerns that transfer across stacks:

- distributed workflow reliability
- idempotent message processing
- duplicate-delivery protection
- retry and dead-letter behavior
- operational diagnostics
- role-based authorization
- SQL-backed auditing
- production-oriented automated testing
- frontend visibility for backend systems
- maintainable and accurately scoped documentation

The expansion strengthens the project as evidence of practical C#/.NET backend development while keeping its limitations and claims fully defensible.
