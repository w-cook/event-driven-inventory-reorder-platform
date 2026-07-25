# Inventory Operations and Reliability Expansion — Case Study

## Context

The original Event-Driven Inventory Reorder Platform demonstrated a distributed backend workflow with an ASP.NET Core API, background worker, SQL-backed state, Docker-based local development, and queue-based reorder processing.

That foundation is useful, but real operational systems also need visibility, reliability, authorization, and debugging support.

## Expansion Goal

This expansion adds production-readiness concerns to the existing system without replacing the original architecture.

The goal is to show how a backend workflow can evolve from a functional event-driven demo into a more operationally useful system.

## Key Engineering Themes

### Operator Visibility

A React/TypeScript dashboard provides visibility into inventory items, low-stock conditions, reorder state, processing history, and system health.

### Role-Aware Workflows

The API now uses a small demo authentication scheme and policy-based authorization to model three operational roles:

- `Viewer` can read inventory, reorder workflow, and system-health data.
- `Operator` can perform the same read operations and can create or update inventory items.
- `Administrator` can perform operational actions and inspect the audit trail.

The React dashboard identifies itself as using the Operator demo role and sends the corresponding demo-user header through a shared frontend API client.

The authentication mechanism is intentionally local and portfolio-focused. It demonstrates ASP.NET Core authentication handlers, claims, roles, and authorization policies without claiming integration with a production identity provider.

### Audit Trail

Successful inventory creation and update operations now create SQL-backed audit records.

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
```

### Reliable Message Processing

The processor will be hardened against duplicate messages, transient failures, and failed-processing scenarios.

### Observability

Structured logs, correlation identifiers, health checks, and trace examples will make the system easier to debug and explain.

### Production-Oriented Testing

The expansion will add tests around duplicate delivery, authorization, failed processing, and end-to-end reorder behavior.

## Portfolio Value

This project phase demonstrates practical backend engineering concerns that transfer across stacks:

- distributed workflow reliability
- operational diagnostics
- authorization
- automated testing
- frontend visibility for backend systems
- maintainable production-style documentation