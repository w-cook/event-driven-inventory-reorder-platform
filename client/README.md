# Inventory Operations Dashboard

React/TypeScript frontend for the Event-Driven Inventory Reorder Platform.

The client provides an authenticated operations dashboard for inventory visibility, low-stock review, configured reorder quantities, inventory creation and updates, reorder-request snapshots, processing history, application health, Administrator audit review, and Administrator account management. Inventory and workflow business rules remain in the ASP.NET Core API.

## Features

- application login and logout with in-memory JWT bearer authentication
- signed-in user and role visibility
- inventory and workflow summary metrics
- inventory table with stock, thresholds, configured reorder quantities, and backend-derived status
- low-stock filtering
- Operator and Administrator inventory creation and editing
- readable mutation validation and API errors
- automatic inventory, workflow, summary, and health refresh after mutations
- reorder-event history with quantity-at-trigger and requested-quantity snapshots
- application and database health with manual refresh
- Administrator-only audit review with formatted, expandable details
- Administrator account creation, role changes, and activation controls
- independent loading, empty, success, and error states

The API remains the authoritative security and business-rule boundary. Frontend role checks improve usability by hiding unavailable actions, while protected endpoints independently enforce the same authorization requirements.

## Authentication Model

The client authenticates through:

```http
POST /api/auth/login
```

A successful response includes a signed JWT access token, the authenticated email address, and assigned roles.

The shared API client automatically sends the token on protected requests:

```http
Authorization: Bearer <access-token>
```

The token is stored only in frontend module memory. It is not written to `localStorage`, `sessionStorage`, cookies, or tracked configuration files.

As a result:

- refreshing the page clears the current client session
- closing the page clears the current client session
- the user must sign in again after a refresh
- the project does not currently implement refresh tokens or persistent browser sessions

Authentication credentials and JWT signing settings belong to the ASP.NET Core API configuration. They must never be placed in frontend environment files.

## Role-Aware Behavior

The API remains the final authority for authentication and authorization. The frontend uses returned role information only to control navigation and action visibility.

| Role | Client behavior |
| --- | --- |
| `Viewer` | Can view inventory, reorder history, workflow summaries, and system health |
| `Operator` | Viewer access plus inventory-item creation and editing |
| `Administrator` | Operator access plus audit-record review and application-account management |

The Administrator interface supports:

- reviewing audit records for successful inventory and account-management actions
- viewing the user, role, action, affected entity, and occurrence time for each audit record
- expanding formatted audit details to inspect action-specific changes
- manually refreshing the audit trail after operational or administrative actions
- viewing account email, role, status, and creation date
- creating password-protected accounts
- changing account roles
- deactivating active accounts
- reactivating inactive accounts

The currently signed-in Administrator row is labeled as the current session rather than presenting role or activation controls. The backend also prevents the final active Administrator from being demoted or deactivated.

Role or activation changes invalidate previously issued tokens for the affected account through the server-side security-stamp check.

When an authenticated API request returns `401 Unauthorized`, the client clears its in-memory access token and authenticated state, returns to the login form, and explains that the session is no longer valid. Standard `403 Forbidden` responses are converted into a readable permission message.

## Backend Dependency

The client calls protected `/api` endpoints exposed by `InventoryReorderPlatform.Api`.

Current client coverage includes:

```text
POST   /api/auth/login
GET    /api/inventoryitems
POST   /api/inventoryitems
PUT    /api/inventoryitems/{id}
GET    /api/reorderevents
GET    /api/operations/health
GET    /api/audit-records
GET    /api/accounts
POST   /api/accounts
PATCH  /api/accounts/{id}/role
PATCH  /api/accounts/{id}/status
```

The API also exposes individual inventory lookup and infrastructure health endpoints for other consumers and operational tooling. See the repository-level README and API documentation for the broader surface.

## Install Dependencies

From the `client` directory:

```bash
npm install
```

## Running with Aspire

Aspire is the preferred development mode because it starts the client with the API, Processor, and application SQL Server and provides resource health, logs, metrics, and traces.

From the repository root, first start the external Service Bus Emulator dependencies:

```bash
docker compose -f docker-compose.local.yml up -d sb-emulator-sql servicebus-emulator
```

On Windows Command Prompt:

```cmd
docker compose -f docker-compose.local.yml up -d sb-emulator-sql servicebus-emulator
```

Then start the AppHost:

```bash
dotnet run --project InventoryReorderPlatform.AppHost
```

The AppHost starts the Vite client automatically.

Open the `client` endpoint from the Aspire dashboard. Aspire injects the active API endpoint into the Vite process, so no manual API proxy URL is required.

Sign in with an application account configured through the API. The initial Administrator is created through the API's local bootstrap configuration described in the repository-level [README](../README.md).

Do not also run `npm run dev` while the Aspire-managed client is active. Vite uses a strict port, and a second instance may conflict with the Aspire-managed process.

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

The Docker/local API must be configured with the same JWT and bootstrap-account settings described in the repository-level README.

## Environment Configuration

Create `client/.env.local` only when a frontend development override is needed. `client/.env.example` contains the supported settings.

Example:

```env
VITE_API_PROXY_TARGET=http://localhost:8080
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

### `VITE_PORT`

Overrides the Vite development-server port.

Default:

```text
5173
```

The development server uses `strictPort`, so startup fails rather than silently selecting another port when the configured port is unavailable.

Do not place account passwords, JWT signing keys, bootstrap credentials, or access tokens in `.env.local` or `.env.example`.

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
├── api/          Authenticated HTTP client and endpoint request functions
├── components/   Login, inventory, workflow, health, audit, and administration UI
├── types/        Authentication, account, inventory, audit, and workflow API types
├── App.tsx       Session state, role-aware composition, loading, and refresh coordination
└── App.css       Layout, forms, tables, badges, and responsive presentation
```

Important client responsibilities include:

- `src/api/httpClient.ts` keeps the current access token in module memory, attaches the bearer header, and handles unauthorized responses
- `src/api/auth.ts` submits login credentials
- `src/api/accounts.ts` performs Administrator account-management requests
- `src/api/inventoryItems.ts` performs protected inventory listing, creation, and update requests
- `src/api/auditRecords.ts` retrieves Administrator-only audit records
- `src/components/LoginForm.tsx` handles unauthenticated login state
- `src/components/InventoryItemForm.tsx` handles inventory creation and editing
- `src/components/AuditRecordsPanel.tsx` displays successful inventory and account-management actions
- `src/components/AccountManagementPanel.tsx` lists accounts and exposes role and activation controls
- `src/components/CreateAccountForm.tsx` handles Administrator account creation
- `src/App.tsx` coordinates authenticated state, logout, invalidated-session handling, role-aware rendering, dashboard loading, and data refresh after inventory mutations

## Data Loading

After authentication, the dashboard loads inventory items and reorder events together. The system-health panel maintains its own loading and error state so it can fail or refresh independently without hiding inventory and workflow data.

After a successful inventory creation or update, the client reloads inventory items and reorder events so summaries, low-stock visibility, workflow history, and the edited item reflect authoritative backend state. It also refreshes system health independently.

Inventory status remains calculated by the backend. The low-stock filter operates on inventory data already returned by the API.

The Administrator audit panel and account-management panel load only for an authenticated Administrator. Viewer and Operator sessions do not render those panels or call their protected endpoints.

After account creation, role changes, or activation changes, the account-management view updates its displayed data and reports the result to the Administrator. Audit records can then be reloaded through the audit panel’s refresh control.

## Error Handling

The shared HTTP response handling converts ASP.NET Core problem-details and validation responses into readable client messages.

The interface reports errors for:

- invalid login credentials
- invalid inventory values
- duplicate account email addresses
- weak passwords and unsupported roles
- forbidden inventory, audit, or account-management access
- attempts to demote or deactivate the final active Administrator
- unavailable API or database dependencies

A `401 Unauthorized` response clears the in-memory token and authenticated state, returns the user to the login form, and explains that the session is no longer valid. A `403 Forbidden` response retains the session and presents a permission message.

The API remains responsible for enforcing all security and business constraints even when the frontend hides or disables an action.

## Current Limitations

The client intentionally does not provide:

- persistent browser sessions or refresh tokens
- dead-letter replay controls
- supplier or purchasing workflows
- automatic stock receipt
- production enterprise identity-provider integration
- separate routed dashboard, inventory, workflow, audit, and administration views

Separate application views and broader layout, responsive, accessibility, and interaction polish are planned for the frontend information-architecture phase.

For complete architecture, backend behavior, local infrastructure, screenshots, testing, and documentation links, see the repository-level [README](../README.md).
