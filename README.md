# Event-Driven Inventory Reorder Platform

A .NET portfolio project focused on event-driven workflow, background processing, relational data modeling, and cloud/container-aligned application architecture.

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
- container-oriented development
- cloud-aligned .NET application architecture

## Project Concept

The application models a small internal inventory platform.

Inventory items have stock levels and reorder thresholds. When stock falls below the configured threshold, the system creates reorder-related records and prepares them for background processing. A separate processor service consumes pending reorder events and marks them as processed.

The project is intentionally scoped like a lightweight internal business platform rather than a polished product.

## What This Project Should Demonstrate

This project is meant to show that I can build more than standard CRUD or server-rendered business forms.

It is intended to support roles involving:
- C# / .NET backend development
- ASP.NET Core application development
- Web API development
- background processing and worker services
- cloud-aligned application architecture
- SQL-backed internal business systems
- event-driven workflow thinking

## Planned Tech Stack

- C#
- ASP.NET Core Web API
- Worker Service
- .NET Aspire
- Entity Framework Core
- SQL Server
- Docker
- Azure Container Apps (deployment target)
- Azure Service Bus (planned event transport)

## Architecture

The solution is structured as a small multi-project distributed application.

Projects:
- `InventoryReorderPlatform.AppHost`
- `InventoryReorderPlatform.ServiceDefaults`
- `InventoryReorderPlatform.Api`
- `InventoryReorderPlatform.Processor`
- `InventoryReorderPlatform.Data`

### AppHost
Orchestrates the distributed application locally during development.

### Api
Provides the main application surface for inventory item management, reorder visibility, and administrative workflows.

### Processor
Runs background logic for reorder event handling and inventory workflow processing.

### Data
Holds the shared EF Core data layer used by both the API and the Processor, including:
- entity models
- `AppDbContext`

## Current Features

Implemented so far:
- distributed .NET solution structure using Aspire
- shared data layer used by both the API and Processor
- SQL-backed persistence with Entity Framework Core
- initial relational data model for:
  - inventory items
  - reorder events
  - reorder history
- `AppDbContext` created and shared across projects
- SQL Server / LocalDB connection configured
- initial migration created and database generated
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
- background processor that:
  - polls for pending reorder events
  - processes them in batches
  - marks them as processed
  - logs processing activity
- end-to-end producer/consumer workflow running under Aspire:
  - API creates pending reorder events
  - Processor consumes and processes them

## Core Workflow

The current workflow is:

1. inventory items are created and tracked
2. each item has a quantity on hand and a reorder threshold
3. when stock falls below threshold, the item transitions to `ReorderPending`
4. a reorder event is created with `Status = "Pending"`
5. the Processor finds pending reorder events in the background
6. the Processor marks those events as `Processed`
7. reorder activity and status changes remain recorded in history

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
- Reorder event status is now separate from inventory item status:
  - reorder event status = `Pending` / `Processed`
  - inventory item status = `Active` / `ReorderPending`
- Reorder events are created only when an item transitions into `ReorderPending`, which avoids duplicate event creation on repeated low-stock updates.
- Reorder history entries are created whenever item status changes.
- The Processor currently uses the shared EF Core data layer and database polling to process reorder events.
- Background processing is currently database-driven; Azure Service Bus integration is planned as a later enhancement.
- The solution uses SQL Server LocalDB for development.
- Aspire is used as the foundation for the distributed application skeleton and local multi-project orchestration.

## Scope Rules

This project should stay compact, employer-facing, and meaningfully different from the earlier support/request-style portfolio work.

### In scope
- inventory tracking workflow
- reorder threshold logic
- background processing
- relational data modeling
- event-driven workflow
- container-aware project structure
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
- cloud/container-aligned .NET architecture
- a different business domain from tickets, service requests, or support portals

## Current Status

Implementation in progress.

Completed so far:
- project direction finalized
- distributed solution structure created
- Aspire-based application skeleton created
- API, Processor, and shared Data projects added to the solution
- initial domain models created
- EF Core data layer created and shared across projects
- SQL Server connection configured
- initial migration created and database applied
- inventory API workflow implemented and smoke-tested
- reorder status logic added to create and update workflows
- automatic reorder event and reorder history generation implemented and tested
- reorder event inspection endpoint implemented
- Processor connected to the shared database and processing pending reorder events in the background