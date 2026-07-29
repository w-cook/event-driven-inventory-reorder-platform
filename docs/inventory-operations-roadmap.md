# Inventory Operations and Reliability Expansion

## Purpose

This phase expands the Event-Driven Inventory Reorder Platform from a backend/event-driven workflow demo into a more production-oriented inventory operations system.

The goal is to demonstrate practical backend reliability, role-aware operations, observability, and an operator-facing dashboard while preserving the original event-driven architecture.

## Why This Expansion Exists

The original project demonstrated API and background processor separation, SQL-backed workflow state, Docker-based local development, and queue-based reorder processing.

This expansion focuses on production-readiness concerns that commonly appear in real backend systems:

- operator visibility
- role-based access
- idempotent message handling
- failed processing recovery
- structured logging
- correlation identifiers
- health/readiness checks
- operational documentation
- higher-value automated tests

## Planned Scope

### Phase 1 — Operations Dashboard Foundation

- [x] React/TypeScript dashboard scaffold
- [x] inventory item list
- [x] low-stock filtering
- [x] reorder status visibility
- [x] processing history view
- [x] system health/status view

### Phase 2 — Authorization and Audit Trail

- [x] role-based access model
- [x] Viewer role for read-only access
- [x] Operator role for quantity updates and workflow actions
- [x] Administrator role for privileged audit-record review
- [x] audit records for important user actions

### Phase 3 — Reliable Message Processing

- [x] idempotent message consumption
- [x] duplicate-message protection
- [x] retry behavior for processor failures
- [x] failed-processing or poison-message handling
- [x] tests proving duplicate delivery does not create duplicate business results

### Phase 4 — Observability

- [x] structured logging
- [x] correlation identifiers across API and processor
- [x] health/readiness endpoints
- [x] OpenTelemetry traces where practical
- [x] documented examples for debugging a reorder workflow

### Phase 5 — Production-Oriented Tests and Documentation

- [x] authorization tests
- [x] duplicate-message tests
- [x] processor failure/recovery tests
- [x] end-to-end API-to-processor workflow test with isolated dependencies
- [x] architecture diagram
- [x] failure scenarios
- [x] reliability decisions and tradeoffs
- [x] operational runbook

### Phase 6 — User Accounts and JWT Authentication

- [ ] persistent application-user accounts
- [ ] securely hashed passwords using established ASP.NET Core identity components
- [ ] login endpoint that issues signed JWT access tokens
- [ ] replace `X-Demo-User` authentication with JWT bearer authentication
- [ ] bootstrap the initial Administrator through environment-based configuration
- [ ] prevent public or anonymous account registration
- [ ] Administrator-only account creation
- [ ] Administrator-only role assignment and changes
- [ ] Administrator-only account deletion or deactivation
- [ ] safeguards against removing or demoting the final Administrator
- [ ] audit records for account and role-management actions
- [ ] authentication and account-management integration tests
- [ ] Swagger/OpenAPI bearer-token configuration

### Phase 7 — Reorder Quantity and Inventory Configuration

- [ ] add a configured reorder quantity to inventory items
- [ ] copy the reorder quantity into each reorder event when triggered
- [ ] include the requested reorder quantity in `ReorderRequestedMessage`
- [ ] persist the requested quantity independently of later inventory changes
- [ ] update create and edit request validation
- [ ] add the required EF Core migration
- [ ] update API responses and frontend types
- [ ] display requested reorder quantities in workflow views
- [ ] extend processor and workflow tests for the new field
- [ ] clearly distinguish requested reorder quantity from stock received

### Phase 8 — Privileged Operations and Administration UI

- [ ] frontend login and logout workflow
- [ ] authenticated API client using JWT bearer tokens
- [ ] role-aware navigation and action visibility
- [ ] Operator and Administrator inventory quantity updates
- [ ] inventory create and edit forms
- [ ] validation and API error handling for inventory mutations
- [ ] Administrator audit-record view
- [ ] Administrator user-account management view
- [ ] account creation, role management, and account deletion controls
- [ ] clear handling of unauthorized and forbidden responses
- [ ] refresh affected dashboard data after successful mutations
- [ ] frontend tests for role-aware behavior where practical

### Phase 9 — Frontend Information Architecture and UX Polish

- [ ] separate dashboard, inventory, workflow, audit, and administration views
- [ ] improve page hierarchy, spacing, and information density
- [ ] improve wide-table readability and responsive behavior
- [ ] add consistent loading, empty, success, and error states
- [ ] improve form usability and destructive-action confirmation
- [ ] add accessible labels, focus behavior, and keyboard navigation
- [ ] improve role and session visibility
- [ ] verify the interface at common desktop and narrow-screen widths
- [ ] refresh screenshots after the final layout is complete

### Phase 10 — Complete API Documentation and Final Verification

- [ ] document every public API endpoint
- [ ] document authentication and authorization requirements
- [ ] document request and response models
- [ ] document validation rules and expected status codes
- [ ] add OpenAPI response metadata and examples where practical
- [ ] document correlation-header behavior
- [ ] document health, audit, user-management, and workflow endpoints
- [ ] update the architecture document for JWT and administration features
- [ ] update the operational runbook for account bootstrap and token usage
- [ ] run the full automated test suite
- [ ] verify both Aspire and Docker/local runtime modes
- [ ] complete a final README, screenshots, and portfolio-claims review


## Non-Goals

- paid cloud deployment
- production hosting
- Kubernetes
- public self-service registration
- third-party enterprise identity-provider integration
- complex refresh-token or single-sign-on infrastructure unless later justified
- replacing the existing backend architecture
- rewriting the worker from scratch
- changing the project into a generic inventory CRUD app

## Success Criteria

This expansion is successful if the project clearly demonstrates:

- practical operations UI
- secure role-aware workflows
- reliable event/message processing
- observable distributed workflow behavior
- documented failure scenarios and recovery expectations
- test coverage for production-style concerns