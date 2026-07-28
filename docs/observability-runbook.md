# Observability Runbook

Use this runbook to trace a reorder workflow across the API, Service Bus, and Processor.

For expected system behavior during duplicate delivery, retries, dead-lettering, publication failures, and dependency outages, see `failure-scenarios.md`.

## Prerequisites

Start the Service Bus Emulator:

```bash
docker compose -f docker-compose.local.yml up -d sb-emulator-sql servicebus-emulator
```

Start the Aspire application:

```bash
dotnet run --project InventoryReorderPlatform.AppHost
```

Wait for these Aspire resources to become available:

* `sql`
* `inventorydb`
* `api`
* `processor`
* `client`

## Trigger a Correlated Reorder Workflow

Open:

```text
InventoryReorderPlatform.Api/InventoryReorderPlatform.Api.http
```

Use one of the existing requests that causes an inventory item to enter `ReorderPending`.

The message-producing requests include explicit correlation headers such as:

```http
X-Correlation-Id: {{initialReorderCorrelationId}}
```

The `.http` file assumes an empty database and top-to-bottom execution when using its fixed identifiers and expected counts.

Confirm that:

* the API request succeeds
* the response includes the same `X-Correlation-Id`
* a reorder event is created
* the reorder event eventually becomes `Processed`

## Search the API Logs

In the Aspire dashboard:

1. Open the API resource.
2. Open its logs.
3. Search for the correlation identifier used in the request.

Confirm that the publication entry includes:

* correlation identifier
* stable Service Bus message id
* reorder-event id

A successful publication log should contain wording similar to:

```text
Published reorder message
```

If the request created a reorder event but no publication log appears, inspect the API error logs for a publication failure.

## Search the Processor Logs

Open the Processor logs and search for the same correlation identifier.

A successful workflow should contain:

```text
Received reorder message
Processed reorder message
Completed processed message
```

Confirm that the entries share:

* the same correlation identifier
* the same stable message id
* the expected reorder-event id

Other possible lifecycle entries include:

```text
Completed duplicate message
Abandoned message
Dead-lettered message
```

The meaning and recovery expectations for those outcomes are documented in `failure-scenarios.md`.

## Inspect the Distributed Trace

Open the Aspire **Traces** view.

Locate the related inventory request by searching for either:

```text
POST /api/inventoryitems
```

```text
PUT /api/inventoryitems
```

or the custom workflow activity:

```text
PublishReorderMessage
```

The application-owned trace flow should resemble:

```text
POST or PUT /api/inventoryitems
└── PublishReorderMessage
    └── ProcessReorderMessage
```

Depending on instrumentation and timing, the display may contain additional framework or messaging spans.

## Inspect Trace Attributes

Open the producer and consumer activities and inspect the available attributes.

Relevant attributes include:

```text
correlation.id
messaging.system
messaging.destination.name
messaging.message.id
messaging.operation.name
messaging.delivery.count
inventory.item.id
reorder.event.id
reorder.outcome
messaging.settlement
```

Not every attribute applies to every span.

The producer activity should identify the publication operation and message destination.

The consumer activity should identify the processing result and settlement outcome.

## Verify Health

Use the Aspire resource health indicators for infrastructure-level status.

The shared service defaults also expose:

```http
GET /health
GET /alive
```

For dashboard-oriented application status, use:

```http
GET /api/operations/health
X-Demo-User: viewer
```

Confirm that the operations response reports:

* API status
* database connectivity
* inventory-item count
* reorder-event count
* UTC check time

## Troubleshooting

### API Starts and Then Exits

Inspect the API logs for dependency-injection validation errors.

A singleton service cannot depend directly on a scoped service. Correlation services used by the singleton message publisher must have compatible lifetimes.

After correcting the registration, run:

```bash
dotnet build
dotnet test
```

Then restart the AppHost.

### API Logs Appear but Processor Logs Do Not

Confirm that:

* the Service Bus Emulator is running
* the Processor resource is healthy
* the API and Processor use the same queue name
* the request actually caused a transition into `ReorderPending`

Updating an item that is already `ReorderPending` does not create another reorder event or publish another message.

### Correlation Appears Only in Some Logs

Search using the complete correlation value.

Important lifecycle entries include the correlation identifier directly in their message templates. Other entries may expose it only through structured logging-scope properties.

Open the log-entry details and inspect its structured fields when necessary.

### Producer and Consumer Spans Are Disconnected

Confirm that the Service Bus message contains:

```text
traceparent
```

and, when present:

```text
tracestate
```

Confirm that the Processor reconstructs its parent context from the incoming `traceparent` value before starting `ProcessReorderMessage`.

### Custom Workflow Spans Do Not Appear

Confirm that the shared OpenTelemetry configuration registers:

```text
InventoryReorderPlatform.Workflow
```

through `AddSource`.

Also confirm that the API and Processor are exporting telemetry to the Aspire dashboard.

### Reorder Event Remains Pending

Search the API logs for the correlation identifier.

Then check for:

1. a successful publication log
2. a Processor receipt log
3. a processing or failure log

This sequence helps determine whether the workflow stopped during publication, delivery, or processing.

See `failure-scenarios.md` for the expected behavior of each failure category.

### Service Bus Emulator Does Not Become Ready

Inspect:

```bash
docker compose -f docker-compose.local.yml logs servicebus-emulator
docker compose -f docker-compose.local.yml logs sb-emulator-sql
```

A stale Service Bus Emulator database file may prevent initialization.

Remove only the emulator-related containers:

```bash
docker compose -f docker-compose.local.yml rm -s -f servicebus-ready servicebus-emulator sb-emulator-sql
```

Then restart the stack.

Avoid removing the application SQL volume unless a complete application-data reset is intended.

## Shutdown

Stop the Aspire AppHost with `Ctrl+C`.

Then stop the local containers:

```bash
docker compose -f docker-compose.local.yml down
```

Do not add `-v` unless a full data reset is intended.