# Inventory Operations and Reliability Expansion

## Purpose

This roadmap tracks the expansion of the Event-Driven Inventory Reorder Platform from a backend workflow demo into a more production-oriented internal operations system.

The work preserves the original event-driven architecture while adding operator visibility, role-aware workflows, reliability controls, observability, automated verification, and employer-facing documentation.

## Current Status

Phases 1–8 are complete. Phase 9 covers frontend information architecture and UX polish; Phase 10 covers complete API documentation and final verification.

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

- [x] add a configured reorder quantity to inventory items
- [x] copy the reorder quantity into each reorder event when triggered
- [x] include the requested reorder quantity in `ReorderRequestedMessage`
- [x] persist the requested quantity independently of later inventory changes
- [x] update create and edit request validation
- [x] add the required EF Core migration
- [x] update API responses and frontend types
- [x] display requested reorder quantities in workflow views
- [x] extend processor and workflow tests for the new field
- [x] clearly distinguish requested reorder quantity from stock received
- [x] update all affected documentation and refresh screenshots where the frontend changed

### Phase 8 — Privileged Operations and Administration UI

- [x] frontend login and logout workflow
- [x] authenticated API client using JWT bearer tokens
- [x] role-aware panel and action visibility
- [x] Operator and Administrator inventory quantity updates
- [x] inventory create and edit forms
- [x] validation and API error handling for inventory mutations
- [x] Administrator audit-record view
- [x] Administrator user-account management view
- [x] account creation, role management, and account activation controls
- [x] clear handling of expired or invalidated sessions and forbidden actions
- [x] refresh affected dashboard data after successful inventory mutations
- [x] verify Viewer, Operator, Administrator, and invalidated-session behavior through a manual role matrix supported by API authorization integration tests
- [x] update all affected documentation and refresh screenshots where the frontend changed

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
