# Inventory Operations and Reliability Expansion — Case Study

## Context

The original Event-Driven Inventory Reorder Platform demonstrated a distributed backend workflow with an ASP.NET Core API, background worker, SQL-backed state, Docker-based local development, and queue-based reorder processing.

That foundation was useful, but practical operational systems also need authenticated user accounts, role-aware access, auditability, operator visibility, failure handling, and protection against duplicate message delivery.

## Expansion Goal

This expansion adds production-oriented concerns to the existing system without replacing the original event-driven architecture.

The goal is to show how a functional backend demo can evolve into a more operationally useful internal business system while keeping the implementation practical, locally reproducible, and accurately scoped.

## Key Engineering Themes

### Authenticated Operator Visibility

The React/TypeScript dashboard turns the protected backend into a usable internal operations interface. It provides:

- authenticated session and role visibility
- inventory, low-stock, workflow, and health summaries
- configured reorder quantities and immutable per-event request snapshots
- Operator and Administrator inventory creation and editing
- dedicated Administrator audit and account-management views
- readable validation, authorization, and invalidated-session handling

Inventory status calculation, reorder-event creation, account validation, authorization, and audit persistence remain backend responsibilities. Viewer sessions are read-only; successful Operator or Administrator mutations reload inventory, workflow, summary, and health data from the API rather than treating optimistic client state as authoritative.

### Frontend Information Architecture and UX Polish

The earlier frontend exposed useful capabilities, but its single-page presentation made the application feel longer and less deliberate as features accumulated. The Phase 9 redesign separated the interface into Dashboard, Inventory, Workflow, Audit, and Administration views while preserving one authenticated application shell.

The redesign favored information density over decorative whitespace because this is an internal operations interface. Summary metrics and System Health share horizontal space on wide screens, cards use compact spacing, and form controls and table rows remain readable without consuming unnecessary vertical space. At narrower widths, navigation and content stack, while wide tables stay contained and become horizontally scrollable instead of forcing the entire page wider.

The hierarchy was standardized around:

- one persistent application title and session area
- one active-view heading and description
- section-level card headings
- consistent action, loading, empty, success, and error presentation

Semantic navigation, active-view indication, accessible labels, visible focus behavior, and keyboard-operable native controls improve usability without introducing a routing framework or duplicating API authorization logic.

This work illustrates a practical tradeoff: the interface is not a consumer marketing site, so compactness and operational scanability matter more than large presentation spacing. The result supports realistic daily use and produces clearer employer-facing screenshots while remaining maintainable in a small React codebase.

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

Role-aware rendering in the React client improves usability, but the API remains the security boundary. Viewer sessions do not receive inventory mutation controls, while Viewer and Operator sessions do not render or request Administrator-only audit or account-management data. Direct unauthorized requests remain rejected by backend policy enforcement.

### Reorder Configuration and Workflow Snapshots

Each inventory item now stores a positive configured `ReorderQuantity` in addition to its current stock and reorder threshold.

When the item enters `ReorderPending`, the API copies that configuration into the new reorder event as `RequestedQuantity`. The same value is included in `ReorderRequestedMessage` and carried through background processing.

This creates a deliberate distinction between mutable inventory configuration and historical workflow state:

- changing `ReorderQuantity` affects future reorder requests
- existing reorder events retain the quantity originally requested
- duplicate delivery and processing recovery continue using the original snapshot
- processing the request does not increase physical stock

This design prevents later configuration changes from silently rewriting business history and keeps the event, message, and persisted processing result aligned.

### Administrator Account Management

Account administration is intentionally controlled rather than exposed through public registration. Authenticated Administrators can list accounts, create password-protected users, assign or change roles, and deactivate or reactivate accounts.

The API prevents the final active Administrator from being demoted or deactivated. These safeguards remain enforced even when requests bypass the React interface.

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

The Administrator frontend provides a dedicated audit-record panel with independent loading, empty, error, and refresh states. Action-specific JSON details are formatted and placed behind expandable controls so detailed change history remains available without overwhelming the table.

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
- positive reorder-quantity validation
- requested-quantity propagation across API, message, Processor, and persistence boundaries
- historical snapshot preservation after later inventory configuration changes

Direct Worker settlement tests are not currently included because the Worker depends on concrete Azure Service Bus transport types. Adding a separate transport abstraction solely for those tests would add more complexity than the current scope requires.

The official Azure Service Bus Emulator remains available for manual producer/consumer verification without requiring paid Azure infrastructure.

### Repeatable Manual Verification

The structured API request file supports a repeatable Aspire-oriented workflow that:

1. logs in as the configured bootstrap Administrator
2. creates Viewer and Operator test accounts
3. reuses named-response access tokens for role-specific requests
4. verifies authorization, reorder quantities, immutable snapshots, inventory changes, audit records, processing, and health behavior

The browser-based role matrix was repeated after the frontend information-architecture work:

- Viewer sessions remained read-only
- Operator sessions received inventory mutation controls but no audit or account-management panels
- Administrator sessions received all privileged panels
- role or activation changes invalidated existing sessions and returned affected users to login on their next protected request

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

This expansion demonstrates practical engineering concerns that transfer across business-application stacks:

- ASP.NET Core Identity and signed JWT bearer authentication
- role-based authorization and controlled account lifecycle management
- immediate invalidation of stale authorization tokens
- role-aware, responsive React operations views and Administrator audit review
- SQL-backed auditing
- distributed workflow reliability and idempotent message processing
- duplicate-delivery protection, retry, and dead-letter behavior
- operational diagnostics and production-oriented automated testing
- mutable configuration versus immutable workflow-snapshot modeling
- maintainable, accurately scoped documentation

The expansion strengthens the project as evidence of practical C#/.NET backend and business-application development while keeping its limitations and claims fully defensible.
