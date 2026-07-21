# Event-Driven Inventory Reorder Platform

A .NET portfolio project focused on event-driven workflow, background processing, relational data modeling, containerized local development, and cloud-ready application architecture.

## Purpose

This project was built to strengthen my backend portfolio with a .NET solution that goes beyond basic CRUD and server-rendered business forms.

The goal was to demonstrate practical experience with:
- ASP.NET Core Web API development
- background processing with a Worker Service
- event-driven workflow design
- Entity Framework Core and SQL Server
- distributed application structure with .NET Aspire
- containerized local development with Docker
- Azure-compatible messaging concepts using the Azure Service Bus Emulator
- zero-cost local infrastructure for development and testing

## Project Concept

The application models a lightweight internal inventory platform.

Inventory items have stock levels and reorder thresholds. When stock falls below the configured threshold, the system creates a reorder event and publishes a reorder message. A separate processor service consumes that message and marks the reorder event as processed.

The project is intentionally scoped like a small internal business platform rather than a polished product or commercial SaaS application.

## What This Project Demonstrates

This project is intended to show that I can build more than simple request/response CRUD applications.

It demonstrates:
- API-first backend design
- background worker processing
- message-driven workflow
- shared data and contracts across projects
- SQL-backed business state
- container-aware local development
- cloud-ready architectural thinking without requiring paid cloud deployment

## Tech Stack

- C#
- ASP.NET Core Web API
- Worker Service
- .NET Aspire
- Entity Framework Core
- SQL Server
- Docker
- Docker Compose
- Azure Service Bus client libraries
- Azure Service Bus Emulator

## Solution Structure

The solution is organized as a small multi-project distributed application.

### `InventoryReorderPlatform.AppHost`
Orchestrates the distributed application locally during development with Aspire.

### `InventoryReorderPlatform.ServiceDefaults`
Holds shared service defaults for the distributed application.

### `InventoryReorderPlatform.Api`
Provides the main application surface for inventory item management and reorder visibility.

### `InventoryReorderPlatform.Processor`
Consumes reorder messages and updates reorder-event processing state.

### `InventoryReorderPlatform.Data`
Holds the shared EF Core data layer, including:
- entity models
- `AppDbContext`

### `InventoryReorderPlatform.Contracts`
Holds shared cross-project contracts, including:
- messaging contracts
- shared configuration classes

## Current Features

- Distributed .NET solution structure using Aspire
- Shared EF Core data layer used by both API and Processor
- Shared contracts layer for messaging and configuration
- SQL-backed persistence with Entity Framework Core
- Relational data model for:
  - inventory items
  - reorder events
  - reorder history
- Inventory API endpoints for:
  - get all inventory items
  - get inventory item by id
  - create inventory item
  - update inventory item
- Reorder event API endpoint for:
  - get all reorder events
- DTO-based request and response flow for the API
- Automatic inventory status calculation based on quantity on hand and reorder threshold
- Automatic status transitions to:
  - `Active`
  - `ReorderPending`
- Automatic reorder event creation when an item transitions into `ReorderPending`
- Automatic reorder history creation when an item status changes
- Duplicate reorder event avoidance when an item remains in the same low-stock state
- Reorder event processing states:
  - `Pending`
  - `Processed`
- Queue-based reorder message publishing from the API
- Queue-based reorder message consumption in the Processor
- End-to-end producer/consumer workflow running locally
- Container-friendly SQL Server development path through Aspire-managed infrastructure
- Docker support for the API and Processor
- Root-level Docker Compose local stack for:
  - application SQL Server
  - Service Bus emulator
  - Service Bus emulator SQL dependency
  - API
  - Processor
- Zero-cost Azure-compatible messaging development using the official Azure Service Bus Emulator

## Expansion

This repository is being expanded through a second portfolio phase: **Inventory Operations and Reliability Expansion**.

The expansion focuses on production-readiness concerns around operator visibility, role-based authorization, reliable message processing, observability, and operational documentation.

Planned additions include a React/TypeScript operations dashboard, idempotent message-processing safeguards, authorization/audit workflows, structured logging, health/readiness checks, and production-oriented tests.

See:

- `docs/project-10-inventory-operations-roadmap.md`
- `docs/inventory-operations-case-study.md`

Implemented for the Inventory Operations and Reliability Expansion so far:

- expansion roadmap
- operational case-study document
- React/TypeScript dashboard scaffold

### Running the Operations Dashboard

From the `client` folder:

```bash
npm install
npm run dev
```

The dashboard is currently an initial React/TypeScript scaffold for the Inventory Operations and Reliability Expansion.

## Core Workflow

1. Inventory items are created and tracked.
2. Each item has a quantity on hand and a reorder threshold.
3. When stock falls below threshold, the item transitions to `ReorderPending`.
4. A reorder event is created with `Status = "Pending"`.
5. The API publishes a `ReorderRequestedMessage`.
6. The Processor consumes that message from the queue.
7. The Processor marks the matching reorder event as `Processed`.
8. Reorder activity and status changes remain recorded in history.

## Data Model

### InventoryItem
- `Id`
- `Name`
- `Sku`
- `QuantityOnHand`
- `ReorderThreshold`
- `Status`
- `CreatedAt`
- `UpdatedAt`

### ReorderEvent
- `Id`
- `InventoryItemId`
- `QuantityAtTrigger`
- `TriggeredAt`
- `Status`

### ReorderHistory
- `Id`
- `InventoryItemId`
- `OldStatus`
- `NewStatus`
- `ChangedAt`

## Screenshots

![Docker local stack running](docs/images/docker-local-stack.png)
![Inventory items endpoint](docs/images/inventory-items-endpoint.png)
![Reorder events showing processed workflow](docs/images/reorder-events-processed.png)
![Processor logs showing queue workflow](docs/images/processor-logs.png)

## Running the Project Locally

This project supports two local run modes.

### Aspire Mode

Best for normal development and debugging.

- Run the `InventoryReorderPlatform.AppHost` project from Visual Studio.
- This starts the distributed app locally through Aspire orchestration.
- For reorder-message publishing and consumption tests, make sure the Service Bus emulator is running.

### Docker / Local Stack Mode

Best for demonstrating the full local containerized distributed system.

From the repository root, run:

```bash
docker compose -f docker-compose.local.yml build
docker compose -f docker-compose.local.yml up -d
docker compose -f docker-compose.local.yml ps
```

The local stack includes:
- application SQL Server
- Service Bus emulator
- Service Bus emulator SQL dependency
- API
- Processor

The API is exposed locally at:

`http://localhost:8080`

Useful commands:

```bash
docker compose -f docker-compose.local.yml logs -f api
docker compose -f docker-compose.local.yml logs -f processor
docker compose -f docker-compose.local.yml logs servicebus-emulator
docker compose -f docker-compose.local.yml down
```

### Important startup note

After starting the Docker local stack, wait until the Service Bus emulator logs show that it is fully up before sending the first POST or PUT request that publishes a reorder message.

## Implementation Notes

- `CreatedAt`, `UpdatedAt`, `TriggeredAt`, and `ChangedAt` are server-controlled timestamps.
- The API uses DTOs for inventory item creation, update, and response shaping.
- Inventory item status is derived from business rules rather than being posted directly by the client.
- The current item status rule is:
  - `QuantityOnHand > ReorderThreshold` → `Active`
  - `QuantityOnHand <= ReorderThreshold` → `ReorderPending`
- Reorder event status is separate from inventory item status:
  - reorder event status = `Pending` / `Processed`
  - inventory item status = `Active` / `ReorderPending`
- Reorder events are created only when an item transitions into `ReorderPending`, which avoids duplicate event creation on repeated low-stock updates.
- Reorder history entries are created whenever item status changes.
- The Processor uses queue-based message consumption as the primary reorder-processing workflow.
- The normal development runtime path is container-friendly rather than LocalDB-dependent.
- Messaging is implemented and tested locally against the official Azure Service Bus Emulator to keep the project zero-cost while staying Azure-compatible.
- The published version is intentionally local and emulator-based rather than deployed to paid Azure resources.

## Local Reset

If I need a clean local restart during development, the simplest reset path is to tear down the Docker local stack and bring it back up fresh.

```bash
docker compose -f docker-compose.local.yml down -v
docker compose -f docker-compose.local.yml up -d --build
```

## Scope

### In scope
- inventory tracking workflow
- reorder threshold logic
- background processing
- relational data modeling
- event-driven workflow
- container-aware project structure
- zero-cost cloud-compatible local development
- clear documentation

### Out of scope
- authentication and authorization
- complex UI polish
- real purchasing integrations
- external vendor APIs
- advanced distributed systems complexity
- anything that turns the project into an oversized platform

## Why This Project Exists

My earlier portfolio projects already demonstrate:
- API development
- MVC server-rendered workflow
- validation-aware form handling
- support-oriented business logic
- relational data handling

This project exists to add proof of:
- background worker design
- event-driven processing
- multi-project distributed application structure
- containerized local infrastructure
- cloud-compatible architecture
- a different business domain from tickets, service requests, or support portals

## Final Positioning

This project is best described as:

- a distributed .NET backend portfolio project
- with an API producer and a background consumer
- backed by SQL Server
- using queue-based reorder processing
- containerized for local execution
- Azure-compatible in architecture
- implemented and tested locally at zero cost using the official Azure Service Bus Emulator

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.