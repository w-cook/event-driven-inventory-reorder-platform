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
- [x] Administrator role for user/configuration management if scope allows
- [x] audit records for important user actions

### Phase 3 — Reliable Message Processing

- [x] idempotent message consumption
- [x] duplicate-message protection
- [x] retry behavior for processor failures
- [x] failed-processing or poison-message handling
- [x] tests proving duplicate delivery does not create duplicate business results

### Phase 4 — Observability

- [ ] structured logging
- [ ] correlation identifiers across API and processor
- [x] health/readiness endpoints
- [ ] OpenTelemetry traces where practical
- [ ] documented examples for debugging a reorder workflow

### Phase 5 — Production-Oriented Tests and Documentation

- [ ] authorization tests
- [ ] duplicate-message tests
- [ ] processor failure/recovery tests
- [ ] end-to-end workflow test with containerized dependencies where practical
- [ ] architecture diagram
- [ ] failure scenarios
- [ ] reliability decisions and tradeoffs
- [ ] operational runbook

## Non-Goals

- paid cloud deployment
- production hosting
- Kubernetes
- overbuilt admin system
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