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

Inventory items have stock levels and reorder thresholds. When stock falls below the configured threshold, the system records a reorder event and processes it through a background workflow.

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

## Planned Architecture

The solution is structured as a small multi-project distributed application.

Projects:
- `InventoryReorderPlatform.AppHost`
- `InventoryReorderPlatform.ServiceDefaults`
- `InventoryReorderPlatform.Api`
- `InventoryReorderPlatform.Processor`

### AppHost
Orchestrates the distributed application locally during development.

### Api
Provides the main application surface for inventory item management, reorder visibility, and administrative workflows.

### Processor
Will run background logic for reorder event handling and inventory workflow processing.

## Current Features

Implemented so far:
- distributed .NET solution structure using Aspire
- API project wired into the distributed solution
- worker project wired into the distributed solution
- SQL-backed persistence with Entity Framework Core
- initial relational data model for:
  - inventory items
  - reorder events
  - reorder history
- `AppDbContext` created and registered
- SQL Server / LocalDB connection configured
- initial migration created and database generated
- first inventory API controller created
- inventory API endpoints for:
  - get all inventory items
  - get inventory item by id
  - create inventory item
- DTO-based request/response flow for the first inventory API workflow

## Planned Core Workflow

The initial workflow is:

1. inventory items are created and tracked
2. each item has a quantity on hand and a reorder threshold
3. when stock falls below threshold, a reorder event is created
4. a background processor handles the reorder event
5. reorder activity and status changes are recorded in history

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
- The initial API uses DTOs for inventory item creation and response shaping.
- The current focus is establishing the domain, data layer, and first working API surface before queue integration and background reorder processing.
- The solution uses SQL Server LocalDB for development.
- Aspire is used as the foundation for the distributed application skeleton.
- The Processor project is currently present as part of the architecture and will be expanded in later implementation steps.

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
- cloud/container-aligned .NET architecture
- a different business domain from tickets, service requests, or support portals

## Current Status

Initial implementation in progress.

Completed so far:
- project direction finalized
- distributed solution structure created
- Aspire-based application skeleton created
- API and Processor projects added to the solution
- initial domain models created
- EF Core data layer created
- SQL Server connection configured
- initial migration created and database applied
- first inventory API workflow implemented and smoke-tested