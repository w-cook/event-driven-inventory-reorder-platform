# Inventory Operations Dashboard

React/TypeScript frontend for the Event-Driven Inventory Reorder Platform.

The client provides an authenticated, role-aware operations interface organized into Dashboard, Inventory, Workflow, Audit, and Administration views. Inventory and workflow business rules remain in the ASP.NET Core API.

## Features

- application login and logout with in-memory JWT bearer authentication
- persistent signed-in user and role visibility
- semantic navigation with active-view indication and keyboard-accessible native controls
- Dashboard view with inventory and current pending, supplier-accepted, and supplier-rejected workflow summary metrics beside system health
- Inventory view with current stock, thresholds, configured reorder quantities, low-stock filtering, and backend-derived status
- Operator and Administrator inventory creation and editing
- Workflow view with quantity-at-trigger and requested-quantity snapshots, supplier status, confirmation details, rejection reasons, and in-place refresh
- Administrator-only Audit view with formatted, expandable details
- Administrator-only Administration view with account creation, role changes, and activation controls
- independent loading, empty, success, and error states
- compact wide-screen presentation with responsive stacking and contained wide tables
- automatic inventory, workflow, summary, and health refresh after successful mutations
- manual Workflow History refresh without a full page reload or lost in-memory session

The API remains the authoritative security and business-rule boundary. Frontend role checks improve usability by hiding unavailable views and actions, while protected endpoints independently enforce the same authorization requirements.

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

## View Structure and Responsive Behavior

The authenticated application uses one persistent shell rather than separate browser routes.

| View | Purpose | Availability |
| --- | --- | --- |
| `Dashboard` | Inventory and workflow summary metrics plus System Health | Viewer, Operator, Administrator |
| `Inventory` | Stock review, low-stock filtering, and inventory create/edit controls | Read for all roles; mutations for Operator and Administrator |
| `Workflow` | Reorder-event quantity history, supplier outcomes, confirmation details, rejection reasons, and refresh | Viewer, Operator, Administrator |
| `Audit` | Successful inventory and account-management actions | Administrator |
| `Administration` | Account creation, role management, and activation controls | Administrator |

On wide screens, the Dashboard places System Health beside the summary-card groups to reduce unnecessary vertical space. Page headers and card spacing use a compact hierarchy suitable for an internal operations tool.

At narrower widths:

- the application header and navigation stack and center
- Dashboard sections return to a single column
- forms collapse to practical single-column layouts
- wide tables remain contained within their cards and can scroll horizontally
- navigation buttons remain keyboard operable and fill the available width

The active view is held in client state and is not currently encoded in the browser URL.

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

The reorder-event response includes current workflow status and optional `supplierOrderId`, `supplierOrderStatus`, `supplierAcceptedAtUtc`, and `supplierRejectionReason` fields. The API also exposes individual inventory lookup and infrastructure health endpoints for other consumers and operational tooling. See the repository-level README and API documentation for the broader surface.

## Install Dependencies

From the `client` directory:

```bash
npm install
```

## Running with Aspire

Aspire is the preferred development mode because it starts the client alongside the inventory API, Processor, mock supplier API, shared local SQL Server resource, and separate inventory and supplier databases while providing resource health, logs, metrics, and traces.

From the repository root, first start the external Service Bus Emulator dependencies:

```bash
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
├── components/   Login, navigation, inventory, workflow, health, audit, and administration UI
├── types/        Authentication, active-view, account, inventory, audit, and workflow types
├── App.tsx       Session state, active-view composition, loading, and refresh coordination
└── App.css       Organized application-shell, view, card, form, table, and responsive styles
```

Important client responsibilities include:

- `src/api/httpClient.ts` keeps the current access token in module memory, attaches the bearer header, and handles unauthorized responses
- `src/api/auth.ts` submits login credentials
- `src/api/accounts.ts` performs Administrator account-management requests
- `src/api/inventoryItems.ts` performs protected inventory listing, creation, and update requests
- `src/api/auditRecords.ts` retrieves Administrator-only audit records
- `src/components/AppNavigation.tsx` renders the role-aware application navigation and active-view state
- `src/components/LoginForm.tsx` handles unauthenticated login state
- `src/components/InventoryItemForm.tsx` handles inventory creation and editing
- `src/components/WorkflowSummaryCards.tsx` counts pending, supplier-accepted, and supplier-rejected events
- `src/components/ReorderWorkflowPanel.tsx` displays supplier outcomes and exposes the Workflow History refresh action
- `src/components/AuditRecordsPanel.tsx` displays successful inventory and account-management actions
- `src/components/AccountManagementPanel.tsx` lists accounts and exposes role and activation controls
- `src/components/CreateAccountForm.tsx` handles Administrator account creation
- `src/types/appView.ts` defines the available application views
- `src/App.tsx` coordinates authenticated state, logout, invalidated-session handling, role-aware view composition, loading, and data refresh

## Data Loading

After authentication, the client loads inventory items and reorder events, then presents the relevant subset through the active view. System Health maintains its own loading and error state so it can fail or refresh independently without hiding summary or workflow information.

After a successful inventory creation or update, the client reloads inventory items and reorder events so Dashboard metrics, low-stock visibility, Workflow history, and the edited item reflect authoritative backend state. It also refreshes System Health independently.

The Workflow History card can reload inventory and reorder-event data on demand. The refresh button is disabled while the request is active, reports errors inside the card, and does not reload the browser page or clear the in-memory JWT session.

Workflow summaries count current `Pending`, `SupplierAccepted`, and `SupplierRejected` events. Legacy `Processed` events may still appear in history for compatibility but are not presented as a current supplier-outcome summary category.

Inventory status remains calculated by the backend. Supplier acceptance does not change `QuantityOnHand`; physical stock changes only through a later inventory update.

Audit and account data load only for an authenticated Administrator. Viewer and Operator sessions do not render those views or call their protected endpoints.

After account creation, role changes, or activation changes, the Administration view updates its displayed data and reports the result. Audit records can then be reloaded through the Audit view’s refresh control.

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
- URL-addressable or deep-linkable application views
- dead-letter replay controls
- supplier-order creation controls independent of the reorder workflow
- shipment, delivery, purchasing, or automatic stock-receipt controls
- password reset, password change, or email-verification workflows
- production enterprise identity-provider integration

For complete architecture, backend behavior, local infrastructure, screenshots, testing, and documentation links, see the repository-level [README](../README.md).

