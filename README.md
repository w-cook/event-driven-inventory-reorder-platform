# Event-Driven Inventory Reorder Platform

A .NET portfolio project focused on event-driven workflow, background processing, and container/cloud-oriented application architecture.

## Purpose

This project is being built to strengthen the portfolio for traditional remote software job applications by covering .NET ecosystem skills that are not shown as clearly in the earlier projects.

The goal is to demonstrate practical experience with:
- ASP.NET Core
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
- background processing and worker services
- cloud-aligned application architecture
- SQL-backed internal business systems
- event-driven workflow thinking

## Planned Tech Stack

- C#
- ASP.NET Core
- Worker Service
- .NET Aspire
- Entity Framework Core
- SQL Server
- Docker
- Azure Container Apps (deployment target)
- Azure Service Bus (planned event transport)

## Planned Architecture

The solution is planned as a small multi-project distributed application.

Initial projects:
- `InventoryReorderPlatform.AppHost`
- `InventoryReorderPlatform.ServiceDefaults`
- `InventoryReorderPlatform.Api`
- `InventoryReorderPlatform.Processor`

### AppHost
Orchestrates the distributed application locally during development.

### Api
Provides the main application surface for inventory item management, reorder visibility, and administrative workflows.

### Processor
Runs background logic for reorder event handling and inventory workflow processing.

## Planned Core Workflow

The initial workflow is:

1. inventory items are created and tracked
2. each item has a quantity on hand and a reorder threshold
3. when stock falls below threshold, a reorder event is created
4. a background processor handles the reorder event
5. reorder activity and status changes are recorded in history

## Planned Data Model

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

Initial setup and planning in progress.

Completed so far:
- project direction finalized
- domain selected
- initial architecture chosen
- distributed .NET solution structure started
- Aspire-based application skeleton created