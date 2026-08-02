# Inventory Operations and Reliability Expansion

## Purpose

This roadmap tracks the expansion of the Event-Driven Inventory Reorder Platform from a backend workflow demo into a more production-oriented internal operations system.

The work preserves the original event-driven architecture while adding operator visibility, role-aware workflows, reliability controls, observability, automated verification, and employer-facing documentation.

## Current Status

Phases 1–9 are complete. Phase 10 adds a mock external supplier service, Phase 11 integrates supplier submission into the event-driven reorder workflow, and Phase 12 completes API documentation and final verification.

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

- [ ] add a separate `InventoryReorderPlatform.SupplierMockApi` ASP.NET Core service
- [ ] define supplier-order request and response contracts
- [ ] implement supplier-order submission
- [ ] require a stable idempotency key for each supplier order
- [ ] return the original supplier order for duplicate idempotency keys
- [ ] validate SKU, requested quantity, and reorder identifiers
- [ ] persist supplier orders independently from inventory-application state
- [ ] support configurable response delay
- [ ] support configurable transient failures
- [ ] support configurable permanent business rejection
- [ ] expose supplier health endpoints
- [ ] add the supplier service to the solution
- [ ] add the supplier service to Aspire orchestration
- [ ] add the supplier service to Docker/local orchestration
- [ ] add automated tests for validation, successful acceptance, and idempotency
- [ ] update all affected documentation

### Phase 11 — Supplier Submission Workflow and Visibility

- [ ] add an `ISupplierOrderClient` abstraction to the Processor
- [ ] implement supplier submission through a typed `HttpClient`
- [ ] configure Aspire service discovery for the supplier client
- [ ] configure the supplier base URL for Docker/local mode
- [ ] propagate correlation identifiers and distributed trace context to the supplier
- [ ] submit the stable Service Bus message ID as the supplier idempotency key
- [ ] submit the supplier order before marking the reorder event processed
- [ ] persist the supplier order identifier and acceptance details
- [ ] keep retryable supplier failures eligible for Service Bus redelivery
- [ ] distinguish retryable failures from permanent supplier rejection
- [ ] add clear terminal reorder states for accepted and rejected requests
- [ ] update reorder-event API responses with supplier-submission information
- [ ] display supplier status and confirmation details in the Workflow view
- [ ] test successful supplier submission
- [ ] test delayed supplier responses
- [ ] test transient supplier failure followed by successful recovery
- [ ] test that message redelivery cannot create duplicate supplier orders
- [ ] test permanent supplier rejection
- [ ] verify traces across API, queue, Processor, and supplier boundaries
- [ ] update all affected documentation and refresh screenshots where the frontend changed

### Phase 12 — Complete API Documentation and Final Verification

- [ ] document every public inventory-platform and supplier API endpoint
- [ ] document authentication and authorization requirements
- [ ] document request and response models
- [ ] document validation rules and expected status codes
- [ ] add OpenAPI response metadata and practical examples
- [ ] document supplier idempotency requirements and outcomes
- [ ] consolidate correlation-header and trace-propagation documentation
- [ ] verify documentation coverage for health, audit, account-management, workflow, and supplier endpoints
- [ ] update the architecture diagram and component responsibilities for the supplier integration
- [ ] consolidate account bootstrap, token usage, supplier configuration, mock behavior, and recovery procedures in the operational runbook
- [ ] run `dotnet build`
- [ ] run the full automated test suite
- [ ] run the frontend production build
- [ ] inspect and verify the generated OpenAPI documents
- [ ] verify normal supplier acceptance
- [ ] verify delayed supplier behavior
- [ ] verify transient failure and recovery
- [ ] verify permanent rejection behavior
- [ ] verify supplier idempotency under duplicate submission
- [ ] verify both Aspire and Docker/local runtime modes
- [ ] verify the complete distributed trace
- [ ] confirm that no secrets or unsafe mock defaults are committed
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
