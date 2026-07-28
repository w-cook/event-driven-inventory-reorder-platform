# Inventory Operations Dashboard

React/TypeScript frontend for the Event-Driven Inventory Reorder Platform.

The dashboard provides operator-facing visibility into inventory state, low-stock conditions, reorder processing, and application health. Backend business rules remain in the ASP.NET Core API.

## Features

- inventory summary metrics
- workflow summary metrics
- inventory table with status badges
- low-stock filtering
- reorder-event processing history
- application and database health panel
- manual health refresh
- independent dashboard and health loading/error states
- configurable local demo identity

The current client is an operations visibility dashboard. Inventory creation and update workflows are exposed by the API but are not currently implemented as frontend forms.

## Backend Dependency

The client calls protected `/api` endpoints and sends an `X-Demo-User` header through the shared API client.

The default identity is:

```text
operator
```

Supported local demo identities are:

- `viewer`
- `operator`
- `admin`

These identities exercise the API's local authentication and authorization model. They are not production user accounts.

## Install Dependencies

From the `client` directory:

```bash
npm install
```

## Running with Aspire

Aspire is the preferred development mode because it starts the client with the API, Processor, and application SQL Server and provides resource health, logs, metrics, and traces.

From the repository root, first start the external Service Bus Emulator dependencies:

```bash
docker compose -f docker-compose.local.yml up -d \
  sb-emulator-sql servicebus-emulator
```

Then start the AppHost:

```bash
dotnet run --project InventoryReorderPlatform.AppHost
```

The AppHost starts the Vite client automatically.

Open the `client` endpoint from the Aspire dashboard. Aspire injects the current API endpoint into the Vite process, so no manual proxy URL is required.

The AppHost currently sets:

```text
VITE_DEMO_USER=operator
```

Do not also run `npm run dev` while the Aspire-managed client is active, because Vite uses a strict port and a second instance may conflict with it.

## Running with the Docker / Local Backend

Start the containerized backend and infrastructure from the repository root:

```bash
docker compose -f docker-compose.local.yml up -d --build
```

Then start Vite from the `client` directory:

```bash
npm run dev
```

Open:

```text
http://localhost:5173
```

When Vite runs outside Aspire, `/api` requests default to:

```text
http://localhost:8080
```

## Environment Configuration

Create `client/.env.local` when a local override is needed. `client/.env.example` contains the common settings.

Example:

```env
VITE_API_PROXY_TARGET=http://localhost:8080
VITE_DEMO_USER=operator
VITE_PORT=5173
```

### `VITE_API_PROXY_TARGET`

Overrides the backend target used by the Vite `/api` development proxy.

Default:

```text
http://localhost:8080
```

Proxy-target precedence is:

1. Aspire-provided `API_HTTP`
2. Aspire-provided `API_HTTPS`
3. `VITE_API_PROXY_TARGET`
4. `http://localhost:8080`

### `VITE_DEMO_USER`

Controls the demo identity sent through `X-Demo-User`.

Supported values:

```text
viewer
operator
admin
```

Default:

```text
operator
```

The dashboard displays the resolved role label in its header.

### `VITE_PORT`

Overrides the Vite development-server port.

Default:

```text
5173
```

The development server uses `strictPort`, so startup fails rather than silently choosing another port when the configured port is unavailable.

## Available Scripts

Run these commands from the `client` directory.

### Start the development server

```bash
npm run dev
```

### Run lint checks

```bash
npm run lint
```

### Create a production build

```bash
npm run build
```

The build runs the TypeScript project build before generating the Vite output.

### Preview the production build

```bash
npm run preview
```

## Frontend Structure

```text
src/
├── api/          Shared API client and endpoint-specific request functions
├── components/   Dashboard cards, tables, workflow, and health components
├── types/        API response and frontend domain types
├── App.tsx       Dashboard composition and data-loading state
└── App.css       Dashboard layout and presentation
```

## Data Loading

The dashboard loads inventory items and reorder events together, while the system-health request maintains its own loading and error state.

This allows the health panel to fail or refresh independently without hiding inventory and workflow data.

The low-stock filter operates on the inventory data already returned by the API. Inventory status itself is calculated by the backend.

## Scope

The frontend currently provides:

- read-only operational visibility
- low-stock review
- reorder-workflow history
- system-health visibility

It does not currently provide:

- production login or account management
- administrator configuration screens
- inventory create/update forms
- dead-letter replay controls
- supplier or purchasing workflows

For the complete architecture, backend behavior, local infrastructure, screenshots, testing, and documentation links, see the repository-level [README](../README.md).
