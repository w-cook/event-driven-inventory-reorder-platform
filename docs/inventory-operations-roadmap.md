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

## Phase Completion Standard

Beginning with Phase 7, every phase concludes with a documentation review so that the `master` branch accurately represents the project after each merge.

The final step of each phase must:

- update all affected project documentation to match the completed implementation
- revise any outdated setup, architecture, workflow, testing, or limitation descriptions
- add or refresh screenshots whenever the frontend has changed
- verify that README and portfolio-facing claims remain accurate and defensible

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

- [x] persistent ASP.NET Core Identity application-user accounts
- [x] securely hashed passwords using established ASP.NET Core Identity components
- [x] login endpoint that issues signed JWT access tokens
- [x] replace `X-Demo-User` authentication with JWT bearer authentication
- [x] bootstrap the initial Administrator through environment-based configuration
- [x] prevent public or anonymous account registration
- [x] Administrator-only account listing and creation
- [x] Administrator-only role assignment and changes
- [x] Administrator-only account deactivation and reactivation
- [x] safeguards against deactivating or demoting the final active Administrator
- [x] immediate token invalidation after account role or status changes
- [x] audit records for account and role-management actions
- [x] authentication and account-management integration tests
- [x] frontend login and logout using in-memory JWT access tokens
- [x] Administrator account-management interface
- [x] update the structured `.http` verification workflow for real JWT authentication


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
- [ ] update all affected documentation and refresh screenshots where the frontend changed

### Phase 8 — Privileged Operations and Administration UI

Several authentication and account-administration interface items originally planned for this phase were completed early as part of Phase 6 and are marked accordingly below.

- [x] frontend login and logout workflow
- [x] authenticated API client using JWT bearer tokens
- [x] role-aware navigation and action visibility
- [ ] Operator and Administrator inventory quantity updates
- [ ] inventory create and edit forms
- [ ] validation and API error handling for inventory mutations
- [ ] Administrator audit-record view
- [x] Administrator user-account management view
- [x] account creation, role management, and account activation controls
- [ ] clear handling of expired, unauthorized, and forbidden sessions
- [ ] refresh affected dashboard data after successful inventory mutations
- [ ] frontend tests for role-aware behavior where practical
- [ ] update all affected documentation and refresh screenshots where the frontend changed


### Phase 9 — Frontend Information Architecture and UX Polish

- [ ] separate dashboard, inventory, workflow, audit, and administration views
- [ ] improve page hierarchy, spacing, and information density
- [ ] improve wide-table readability and responsive behavior
- [ ] add consistent loading, empty, success, and error states
- [ ] improve form usability and destructive-action confirmation
- [ ] add accessible labels, focus behavior, and keyboard navigation
- [ ] improve role and session visibility
- [ ] verify the interface at common desktop and narrow-screen widths
- [ ] update all affected documentation and refresh screenshots where the frontend changed

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