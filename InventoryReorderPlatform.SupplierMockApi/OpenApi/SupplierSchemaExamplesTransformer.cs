using System.Text.Json;
using System.Text.Json.Nodes;
using InventoryReorderPlatform.SupplierMockApi.Contracts;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace InventoryReorderPlatform.SupplierMockApi.OpenApi;

internal sealed class SupplierSchemaExamplesTransformer
    : IOpenApiSchemaTransformer
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        schema.Example =
            context.JsonTypeInfo.Type switch
            {
                var type when
                    type == typeof(CreateSupplierOrderRequest) =>
                    ToNode(
                        new CreateSupplierOrderRequest
                        {
                            ReorderEventId = 1001,
                            InventoryItemId = 25,
                            Sku = "FILTER-100",
                            RequestedQuantity = 20,
                            TriggeredAtUtc =
                                new DateTime(
                                    2026,
                                    8,
                                    4,
                                    12,
                                    30,
                                    0,
                                    DateTimeKind.Utc)
                        }),

                var type when
                    type == typeof(SupplierOrderResponse) =>
                    ToNode(
                        new SupplierOrderResponse
                        {
                            SupplierOrderId =
                                Guid.Parse(
                                    "8d7c6d20-91be-4d08-a104-b831a2295b84"),
                            IdempotencyKey =
                                "reorder-event-1001",
                            ReorderEventId = 1001,
                            InventoryItemId = 25,
                            Sku = "FILTER-100",
                            RequestedQuantity = 20,
                            TriggeredAtUtc =
                                new DateTime(
                                    2026,
                                    8,
                                    4,
                                    12,
                                    30,
                                    0,
                                    DateTimeKind.Utc),
                            Status = "Accepted",
                            AcceptedAtUtc =
                                new DateTime(
                                    2026,
                                    8,
                                    4,
                                    12,
                                    30,
                                    2,
                                    DateTimeKind.Utc)
                        }),

                _ => schema.Example
            };

        return Task.CompletedTask;
    }

    private static JsonNode? ToNode<T>(T value)
    {
        return JsonSerializer.SerializeToNode(
            value,
            JsonOptions);
    }
}