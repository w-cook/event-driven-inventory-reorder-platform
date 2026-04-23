# Event-Driven Inventory Reorder Platform

A .NET portfolio project focused on event-driven workflow, background processing, relational data modeling, containerized local development, and cloud-ready application architecture.

## Purpose

This project is being built to strengthen the portfolio for traditional remote software job applications by covering .NET ecosystem skills that are not shown as clearly in the earlier projects.

The goal is to demonstrate practical experience with:
- ASP.NET Core
- Web API development
- background processing with a worker service
- event-driven workflow design
- Entity Framework Core
- SQL Server
- distributed application structure
- containerized local development
- cloud-compatible messaging architecture
- zero-cost local infrastructure emulation for development and testing

## Project Concept

The application models a small internal inventory platform.

Inventory items have stock levels and reorder thresholds. When stock falls below the configured threshold, the system creates reorder-related records and publishes a reorder message. A separate processor service consumes that message and marks the reorder event as processed.

The project is intentionally scoped like a lightweight internal business platform rather than a polished product.

## What This Project Should Demonstrate

This project is meant to show that I can build more than standard CRUD or server-rendered business forms.

It is intended to support roles involving:
- C# / .NET backend development
- ASP.NET Core application development
- Web API development
- background processing and worker services
- event-driven workflow thinking
- SQL-backed internal business systems
- container-based development
- cloud-ready distributed application design

## Tech Stack

- C#
- ASP.NET Core Web API
- Worker Service
- .NET Aspire
- Entity Framework Core
- SQL Server
- Docker
- Azure Service Bus client libraries
- Azure Service Bus Emulator for local development/testing

## Architecture

The solution is structured as a small multi-project distributed application.

Projects:
- `InventoryReorderPlatform.AppHost`
- `InventoryReorderPlatform.ServiceDefaults`
- `InventoryReorderPlatform.Api`
- `InventoryReorderPlatform.Processor`
- `InventoryReorderPlatform.Data`
- `InventoryReorderPlatform.Contracts`

### AppHost
Orchestrates the distributed application locally during development.

### Api
Provides the main application surface for inventory item management, reorder visibility, and administrative workflows.

### Processor
Runs background logic for reorder message consumption and reorder-event processing.

### Data
Holds the shared EF Core data layer used by both the API and the Processor, including:
- entity models
- `AppDbContext`

### Contracts
Holds shared cross-project contracts, including:
- messaging contracts
- shared configuration classes

## Current Features

Implemented so far:
- distributed .NET solution structure using Aspire
- shared data layer used by both the API and Processor
- shared contracts layer for messaging/configuration
- SQL-backed persistence with Entity Framework Core
- initial relational data model for:
  - inventory items
  - reorder events
  - reorder history
- `AppDbContext` created and shared across projects
- inventory API endpoints for:
  - get all inventory items
  - get inventory item by id
  - create inventory item
  - update inventory item
- reorder event API endpoint for:
  - get all reorder events
- DTO-based request/response flow for the inventory API
- automatic inventory status calculation based on quantity on hand and reorder threshold
- automatic transition to:
  - `Active`
  - `ReorderPending`
- automatic reorder event creation when an item transitions into `ReorderPending`
- automatic reorder history creation when an item status changes
- duplicate reorder event avoidance when an item remains in the same low-stock state
- reorder event processing states:
  - `Pending`
  - `Processed`
- queue-based reorder message publishing from the API
- queue-based reorder message consumption in the Processor
- background processor that:
  - consumes reorder messages from the Service Bus emulator
  - marks matching reorder events as processed
  - logs processing activity
- end-to-end producer/consumer workflow running locally:
  - API creates pending reorder events
  - API publishes reorder messages
  - Processor consumes reorder messages
  - Processor marks reorder events as processed
- container-friendly SQL Server development path through Aspire-managed infrastructure
- Docker support added for:
  - `InventoryReorderPlatform.Api`
  - `InventoryReorderPlatform.Processor`
- zero-cost Azure-compatible messaging development using the official Azure Service Bus Emulator

## Core Workflow

The current workflow is:

1. inventory items are created and tracked
2. each item has a quantity on hand and a reorder threshold
3. when stock falls below threshold, the item transitions to `ReorderPending`
4. a reorder event is created with `Status = "Pending"`
5. the API publishes a `ReorderRequestedMessage`
6. the Processor consumes that message from the queue
7. the Processor marks the matching reorder event as `Processed`
8. reorder activity and status changes remain recorded in history

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
- The Processor now uses queue-based message consumption as the primary reorder-processing workflow.
- The normal development runtime path is container-friendly rather than LocalDB-dependent.
- Docker support has been added for the API and Processor projects.
- Messaging is implemented and tested locally against the official Azure Service Bus Emulator to keep the project zero-cost while staying Azure-compatible.
- The project is cloud-ready in structure, but the published version is intentionally local/emulator-based rather than deployed to paid Azure resources.

## Scope Rules

This project should stay compact, employer-facing, and meaningfully different from the earlier support/request-style portfolio work.

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

The earlier portfolio projects already demonstrate:
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

## Current Status

Implementation in progress.

Completed so far:
- project direction finalized
- distributed solution structure created
- Aspire-based application skeleton created
- API, Processor, shared Data, and shared Contracts projects added to the solution
- initial domain models created
- EF Core data layer created and shared across projects
- inventory API workflow implemented and smoke-tested
- reorder status logic added to create and update workflows
- automatic reorder event and reorder history generation implemented and tested
- reorder event inspection endpoint implemented
- Processor connected to the shared database
- Docker support added to API and Processor
- official Azure Service Bus Emulator integrated for zero-cost local queue development
- queue-based publish/consume workflow implemented and tested end-to-end