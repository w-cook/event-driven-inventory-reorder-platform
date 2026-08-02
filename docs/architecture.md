# System Architecture

## Overview

The Event-Driven Inventory Reorder Platform is a distributed .NET business application with separate user-interface, API, authentication, messaging, background-processing, and persistence responsibilities.

The architecture uses asynchronous message processing for reorder work while preserving SQL-backed business state, ASP.NET Core Identity accounts, audit history, processing records, and failure diagnostics.

The architecture includes separate user-interface, inventory API, authentication, messaging, background-processing, mock external-supplier, observability, and persistence responsibilities.

```mermaid
flowchart LR
    User["Viewer / Operator / Administrator"]
    Developer["Developer / Integration Tests"]

    subgraph Client["Operations Client"]
        React["React + TypeScript Operations Client"]
    end

    subgraph Application["Inventory Platform Services"]
        API["ASP.NET Core Inventory API"]
        Processor[".NET Background Processor"]
    end

    subgraph SupplierBoundary["Mock External Supplier Boundary"]
        SupplierApi["ASP.NET Core Mock Supplier API"]
    end

    subgraph Messaging["Messaging Infrastructure"]
        Queue["Azure Service Bus-Compatible Queue"]
        DLQ["Dead-Letter Queue"]
    end

    subgraph ApplicationPersistence["Inventory Application Database"]
        Identity["Identity Users and Roles"]
        Inventory["Inventory Items"]
        Reorders["Reorder Events and History"]
        Audit["Audit Records"]
        Processed["Processed Message Ledger"]
        Failed["Failed Message Records"]
    end

    subgraph SupplierPersistence["Supplier Database"]
        SupplierOrders["Accepted Supplier Orders"]
    end

    subgraph Observability["Shared Operations and Diagnostics"]
        ServiceDefaults["Service Defaults"]
        Telemetry["Health Checks, Structured Logs,\nMetrics, and OpenTelemetry Traces"]
    end

    User --> React
    React -->|"Login credentials"| API
    API -->|"Signed JWT access token"| React
    React -->|"Bearer-authenticated HTTP requests"| API

    API --> Identity
    API --> Inventory
    API --> Reorders
    API --> Audit
    API -->|"ReorderRequestedMessage"| Queue

    Queue --> Processor
    Processor --> Reorders
    Processor --> Processed
    Processor --> Failed
    Processor -->|"Retryable failure"| Queue
    Processor -->|"Unprocessable or exhausted message"| DLQ

    Developer -->|"Direct HTTP verification"| SupplierApi
    SupplierApi --> SupplierOrders

    ServiceDefaults -.-> API
    ServiceDefaults -.-> Processor
    ServiceDefaults -.-> SupplierApi
    ServiceDefaults -.-> Telemetry

    API -.->|"Correlation ID and trace context"| Queue
    Queue -.->|"Correlation ID and trace context"| Processor
```

## Component Responsibilities

### React Operations Client

The React and TypeScript client provides an authenticated shell with persistent session context, semantic view navigation, and five focused application views:

- Dashboard for inventory and workflow summaries plus system health
- Inventory for stock review, low-stock filtering, and privileged create/edit operations
- Workflow for reorder-event history and quantity snapshots
- Audit for Administrator-only audit review
- Administration for Administrator-only account lifecycle management

The active view is client presentation state rather than an authorization boundary. Viewer, Operator, and Administrator capabilities remain enforced by API policies even when unavailable controls or views are omitted from the interface.

The client keeps the JWT access token in application memory and attaches it to protected requests. A rejected or invalidated token clears authenticated client state and returns the user to the login form.

Inventory and workflow business state remains backend-owned. After successful inventory mutations, the client reloads inventory, reorder-event, and health data so summaries and tables reflect authoritative results.

The presentation layer uses compact cards, consistent view headers, contained wide tables, visible focus treatment, and responsive stacking. These choices affect information delivery only; they do not duplicate inventory, workflow, authentication, or authorization rules.

### ASP.NET Core API

The API is responsible for:

- managing persistent application accounts through ASP.NET Core Identity
- hashing and validating passwords through established Identity components
- creating the Viewer, Operator, and Administrator roles
- optionally bootstrapping the initial Administrator from local configuration
- authenticating credentials through `POST /api/auth/login`
- issuing signed JWT access tokens
- validating token issuer, audience, signature, expiration, account activity, and security stamp
- enforcing Viewer, Operator, and Administrator authorization policies
- preventing public self-service registration
- providing Administrator-only account listing and lifecycle operations
- preventing the final active Administrator from being demoted or deactivated
- validating inventory requests
- managing inventory and reorder state
- validating positive configured reorder quantities
- copying the configured reorder quantity into each new reorder event and message
- recording successful inventory and account-management actions in the audit trail
- creating stable reorder messages
- propagating correlation and trace context

JWT access tokens contain the authenticated account identity, assigned roles, and the Identity security stamp. Role and activation changes update the security stamp, causing previously issued tokens for that account to fail validation immediately.

When an item enters a low-stock state, the API saves the business state before publishing the corresponding reorder message.

The API copies the inventory item’s current `ReorderQuantity` into the new reorder event as `RequestedQuantity`. The message is built from that event snapshot rather than reading mutable inventory configuration again.

### Authentication and Authorization Boundary

The platform uses local application-managed accounts rather than an external identity provider.

There is no anonymous registration endpoint. The first Administrator is configured outside source control, and that Administrator can create later accounts through the protected API or Administrator dashboard.

The three application roles are:

- **Viewer:** read inventory, reorder workflow, and application-health data
- **Operator:** Viewer access plus inventory creation and updates
- **Administrator:** Operator access plus audit-record access and account administration

Authorization remains enforced by the API. Hiding Administrator controls in the React client improves usability but is not treated as the security boundary.

### Message Queue

The Azure Service Bus-compatible queue separates the inventory request from background reorder processing.

Each `ReorderRequestedMessage` carries the requested quantity captured by the originating reorder event.

The platform assumes at-least-once delivery. Stable message identifiers and a SQL-backed processed-message ledger make duplicate delivery harmless.

Retryable failures return to the queue until the configured delivery limit is reached. Invalid or repeatedly failing messages are moved to the dead-letter queue.

### Background Processor

The Processor:

- consumes reorder messages
- verifies that the associated reorder event exists
- rejects unsupported workflow states
- detects previously processed messages
- records successful message processing
- updates valid reorder events to `Processed`
- records failed processing attempts
- coordinates retry and dead-letter behavior

A `Processed` reorder event represents successful internal handling of the reorder request. It does not represent delivery of physical stock.

### Mock Supplier API

The mock supplier API is an independently hosted ASP.NET Core service that provides a realistic external HTTP boundary for development and automated testing.

It:

- accepts supplier-order submissions
- requires an explicit idempotency key
- validates supplier-owned request contracts
- persists accepted orders in its own database
- returns the original accepted result for an identical replay
- rejects conflicting reuse of an idempotency key
- simulates delayed responses, temporary unavailability, and permanent rejection
- exposes health, liveness, and OpenAPI endpoints

The service does not reference the inventory API, inventory persistence project, Processor, or internal Service Bus contract. Its contracts therefore represent an external-service boundary rather than a shared in-process model.

During Phase 10, the service is exercised directly through HTTP integration tests and local verification. The Processor-to-supplier call is intentionally deferred to Phase 11.

### Persistence Boundaries

SQL Server stores the application’s durable state:

- ASP.NET Core Identity users, roles, claims, and account security data
- inventory items, including current reorder configuration
- reorder events, including immutable requested-quantity snapshots
- reorder status history
- audit records
- successfully processed message identifiers
- failed message details

The mock supplier service uses a separate `SupplierDbContext`, migration history, schema, and database for accepted supplier orders.

Aspire and Docker may host both databases through the same local SQL Server resource, but that shared infrastructure does not combine their ownership. The inventory platform and supplier service use separate connection strings, contexts, models, tables, constraints, and migrations.

Identity state, business state, audit state, and message-processing state remain distinct so operators can distinguish account access, inventory conditions, workflow results, and infrastructure outcomes.

### Observability

Shared Service Defaults configure health checks, structured logging, metrics, and OpenTelemetry instrumentation for the inventory API, Processor, and mock supplier API.

During Phase 10, correlation and W3C trace propagation connect the inventory API, queue, and Processor. Direct supplier requests produce their own service telemetry, but they are not yet children of the reorder workflow trace. That connection is introduced in Phase 11.

Each API request accepts or generates an `X-Correlation-Id`. The identifier and W3C trace context are propagated through the message boundary so a reorder workflow can be followed across the API, queue, and Processor.

Authentication and authorization failures remain normal HTTP request outcomes. Sensitive credentials, password hashes, JWT signing keys, and raw bearer tokens must not be written to application logs.

## Local Runtime Modes

The same application architecture supports two local development paths:

- **.NET Aspire:** orchestrates the inventory API, Processor, mock supplier API, React operations client, SQL Server, separate inventory and supplier databases, health information, logs, metrics, and traces.
- **Docker/local mode:** runs the inventory API, Processor, mock supplier API, both databases, and messaging infrastructure through Docker Compose while the React client is started separately.

Both modes use zero-cost local infrastructure and the same Identity, authorization, inventory, and messaging workflow.

Local secrets are supplied outside source control. Aspire development uses ASP.NET Core User Secrets for the JWT signing key, bootstrap Administrator credentials, and structured `.http` test-account password.

## Intentional Boundaries

The current architecture does not claim:

- an external production identity provider or single sign-on integration
- public self-service registration
- refresh-token rotation or persistent browser sessions
- a real production supplier or purchasing integration
- Processor-to-supplier submission during Phase 10
- exactly-once message delivery
- automatic receipt of replacement stock
- paid cloud deployment
- production hosting

The mock supplier service is intentionally a local development and verification boundary. Its existence demonstrates external-service contract design, idempotency, failure simulation, persistence ownership, and orchestration without claiming a commercial supplier relationship or completed purchasing workflow.

These boundaries keep the portfolio project practical while making its security, reliability, and operational claims fully defensible.
