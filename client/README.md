# Inventory Operations Dashboard

React/TypeScript frontend for the Event-Driven Inventory Reorder Platform.

The client provides an authenticated operations dashboard for inventory visibility, low-stock review, configured reorder quantities, reorder-request snapshots, processing history, application health, and Administrator account management. Inventory and workflow business rules remain in the ASP.NET Core API.

## Features

- application login and logout
- JWT bearer authentication through the shared API client
- signed-in user and role visibility
- inventory and workflow summary metrics
- inventory table with current stock, thresholds, configured reorder quantities, and backend-derived status badges
- low-stock filtering
- reorder-event processing history with quantity-at-trigger and requested-quantity snapshots
- application and database health panel
- manual health refresh
- Administrator-only account listing
- Administrator-only account creation
- Administrator role changes
- Administrator account deactivation and reactivation
- role-aware visibility for privileged controls
- independent dashboard, health, and account-management loading and error states

The current client does not yet provide inventory create or edit forms. Those workflows remain available through the authenticated API and structured `.http` verification file until the later privileged-operations UI phase.

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
| `Viewer` | Can view inventory, reorder history, and system health |
| `Operator` | Currently receives the same dashboard visibility as Viewer; inventory mutation UI is planned for a later phase |
| `Administrator` | Receives dashboard visibility plus the account-management panel |

The Administrator account panel supports:

- viewing account email, role, status, and creation date
- creating a password-protected account
- changing an account role
- deactivating an active account
- reactivating an inactive account

The currently signed-in Administrator row is labeled as the current session rather than presenting role or activation controls. The backend also prevents the final active Administrator from being demoted or deactivated.

Role or activation changes invalidate previously issued tokens for the affected account through the server-side security-stamp check.

## Backend Dependency

The client calls protected `/api` endpoints exposed by `InventoryReorderPlatform.Api`.

Current endpoint coverage includes:

```text
POST   /api/auth/login
GET    /api/inventoryitems
GET    /api/reorderevents
GET    /api/operations/health
GET    /api/accounts
POST   /api/accounts
PATCH  /api/accounts/{id}/role
PATCH  /api/accounts/{id}/status
```

Inventory creation, inventory updates, individual inventory lookup, and audit-record review remain API-only workflows for now.

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
├── api/          Shared authenticated API client and endpoint request functions
├── components/   Login, dashboard, workflow, health, and account-management UI
├── types/        Authentication, account, and operational API types
├── App.tsx       Session state, role-aware composition, and dashboard loading
└── App.css       Layout, forms, tables, badges, and responsive presentation
```

Important client responsibilities include:

- `api/httpClient.ts` keeps the current access token in module memory and attaches the bearer header
- `api/auth.ts` submits login credentials
- `api/accounts.ts` performs Administrator account-management requests
- `LoginForm.tsx` handles unauthenticated login state
- `AccountManagementPanel.tsx` lists accounts and exposes role/status controls
- `CreateAccountForm.tsx` handles Administrator account creation
- `App.tsx` clears authenticated and dashboard state during logout

## Data Loading

After authentication, the dashboard loads inventory items and reorder events together. The system-health panel maintains its own loading and error state so it can fail or refresh independently without hiding inventory and workflow data.

The Administrator account panel loads only for an authenticated Administrator. Viewer and Operator sessions do not render the panel or call `/api/accounts`.

The low-stock filter operates on inventory data already returned by the API. Inventory status itself is calculated by the backend.

After a successful account creation, role change, or activation change, the account-management view updates its displayed data and reports the result to the Administrator.

## Error Handling

The shared HTTP response handling converts standard ASP.NET Core problem-details and validation responses into readable client messages.

The frontend displays errors for cases such as:

- invalid login credentials
- duplicate account email addresses
- weak passwords
- invalid role requests
- forbidden account-management access
- attempts to demote or deactivate the final active Administrator
- unavailable API or database dependencies

The API remains responsible for enforcing all security and business constraints even when the frontend hides or disables an action.

## Current Scope

The frontend currently provides:

- authenticated dashboard access
- in-memory bearer-token handling
- role and session visibility
- read-only inventory operations visibility
- low-stock review
- reorder-workflow history
- system-health visibility
- Administrator application-account management

It does not currently provide:

- persistent browser sessions or refresh tokens
- inventory create or update forms
- Administrator audit-record view
- dead-letter replay controls
- supplier or purchasing workflows
- production enterprise identity-provider integration

For complete architecture, backend behavior, local infrastructure, screenshots, testing, and documentation links, see the repository-level [README](../README.md).
