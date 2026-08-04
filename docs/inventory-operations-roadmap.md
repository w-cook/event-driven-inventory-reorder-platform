# Inventory Operations and Reliability Expansion

## Purpose

This roadmap tracks the expansion of the Event-Driven Inventory Reorder Platform from a backend workflow demo into a more production-oriented internal operations system.

The work preserves the original event-driven architecture while adding operator visibility, role-aware workflows, reliability controls, observability, automated verification, and employer-facing documentation.

## Current Status

Phases 1–11 are complete. Phase 12 covers complete endpoint documentation, generated OpenAPI review, and final verification.

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

- [x] separate dashboard, inventory, workflow, audit, and administration views
- [x] improve page hierarchy, spacing, and information density
- [x] improve wide-table readability and responsive behavior
- [x] add consistent loading, empty, success, and error states
- [x] improve form usability and destructive-action confirmation
- [x] add accessible labels, focus behavior, and keyboard navigation
- [x] improve role and session visibility
- [x] verify the interface at common desktop and narrow-screen widths
- [x] update all affected documentation and refresh screenshots where the frontend changed

### Phase 10 — Mock Supplier Service

- [x] independently hosted ASP.NET Core mock supplier service
- [x] supplier-owned request and response contracts
- [x] separate supplier EF Core context and database
- [x] supplier database migration
- [x] required idempotency-key contract
- [x] durable identical-replay behavior
- [x] conflicting-key detection
- [x] database-level idempotency enforcement
- [x] configurable normal and delayed behavior
- [x] configurable transient failures and eventual recovery
- [x] configurable permanent rejection
- [x] health, liveness, and OpenAPI endpoints
- [x] supplier endpoint and persistence integration tests
- [x] Aspire orchestration
- [x] Docker/local orchestration
- [x] durable replay verification across a service restart
- [x] mock supplier contract and operations documentation
- [x] all affected README, architecture, failure-scenario, case-study, observability, and client documentation

### Phase 11 — Supplier Submission Workflow and Visibility

- [x] add an `ISupplierOrderClient` abstraction to the Processor
- [x] implement supplier submission through a typed `HttpClient`
- [x] configure Aspire service discovery for the supplier client
- [x] configure the supplier base URL for Docker/local mode
- [x] propagate correlation identifiers and distributed trace context to the supplier
- [x] submit the stable Service Bus message ID as the supplier idempotency key
- [x] submit the supplier order before recording a terminal reorder outcome
- [x] persist the supplier order identifier and acceptance details
- [x] keep retryable supplier failures eligible for Service Bus redelivery
- [x] distinguish retryable failures from permanent supplier rejection
- [x] add clear terminal reorder states for accepted and rejected requests
- [x] update reorder-event API responses with supplier-submission information
- [x] display supplier status and confirmation details in the Workflow view
- [x] add in-place Workflow History refresh without clearing the login session
- [x] update workflow summary counts for pending, supplier-accepted, and supplier-rejected events
- [x] test successful supplier submission
- [x] test delayed supplier responses
- [x] test transient supplier failure followed by successful recovery
- [x] test that message redelivery cannot create duplicate supplier orders
- [x] test permanent supplier rejection
- [x] verify traces across API, queue, Processor, and supplier boundaries
- [x] update all affected documentation and refresh screenshots where the frontend changed

### Phase 12 — Complete API Documentation and Final Verification

- [x] document every public inventory-platform and supplier API endpoint
- [x] document authentication and authorization requirements
- [x] document request and response models
- [x] document validation rules and expected status codes
- [x] add OpenAPI response metadata and practical examples
- [x] document supplier idempotency requirements and outcomes
- [x] consolidate correlation-header and trace-propagation documentation
- [x] verify documentation coverage for health, audit, account-management, workflow, and supplier endpoints
- [x] update the architecture diagram and component responsibilities for the supplier integration
- [x] consolidate account bootstrap, token usage, supplier configuration, mock behavior, and recovery procedures in the operational runbook
- [x] run `dotnet build`
- [x] run the full automated test suite
- [x] run the frontend production build
- [x] inspect and verify the generated OpenAPI documents
- [x] verify normal supplier acceptance
- [x] verify delayed supplier behavior
- [ ] verify transient failure and recovery
- [ ] verify permanent rejection behavior
- [ ] verify supplier idempotency under duplicate submission
- [ ] verify both Aspire and Docker/local runtime modes
- [x] verify the complete distributed trace
- [x] confirm that no secrets or unsafe mock defaults are committed
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
- integration with a real commercial supplier or purchasing platform
- automatic shipment, delivery, or physical stock receipt
- production-grade supplier credentials, billing, or procurement rules

## Success Criteria

This expansion is successful if the project clearly demonstrates:

- practical, responsive operations UI with focused application views
- secure role-aware workflows
- reliable event/message processing
- observable distributed workflow behavior
- documented failure scenarios and recovery expectations
- test coverage for production-style concerns
- a meaningful external-service boundary that justifies asynchronous processing
- supplier idempotency across message retries and ambiguous processing outcomes
- visible supplier acceptance, rejection, delay, and recovery behavior
- service-to-service HTTP integration with end-to-end correlation and tracing
