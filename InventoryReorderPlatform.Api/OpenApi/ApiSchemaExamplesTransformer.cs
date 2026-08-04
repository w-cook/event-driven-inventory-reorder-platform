using System.Text.Json;
using System.Text.Json.Nodes;
using InventoryReorderPlatform.Api.DTOs;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace InventoryReorderPlatform.Api.OpenApi;

internal sealed class ApiSchemaExamplesTransformer
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
                var type when type == typeof(LoginRequest) =>
                    ToNode(
                        new LoginRequest
                        {
                            Email = "admin@example.local",
                            Password = "<local-password>"
                        }),

                var type when type == typeof(LoginResponse) =>
                    ToNode(
                        new LoginResponse(
                            AccessToken: "<jwt-access-token>",
                            ExpiresAtUtc:
                                new DateTime(
                                    2026,
                                    8,
                                    4,
                                    14,
                                    0,
                                    0,
                                    DateTimeKind.Utc),
                            UserId:
                                "2a8f0348-ec74-4ca3-b5c1-6ad37df17bb5",
                            Email: "admin@example.local",
                            Roles: ["Administrator"])),

                var type when
                    type == typeof(CreateInventoryItemRequest) =>
                    ToNode(
                        new CreateInventoryItemRequest
                        {
                            Name = "Air Filter",
                            Sku = "FILTER-100",
                            QuantityOnHand = 8,
                            ReorderThreshold = 10,
                            ReorderQuantity = 20
                        }),

                var type when
                    type == typeof(UpdateInventoryItemRequest) =>
                    ToNode(
                        new UpdateInventoryItemRequest
                        {
                            Name = "Air Filter",
                            Sku = "FILTER-100",
                            QuantityOnHand = 25,
                            ReorderThreshold = 10,
                            ReorderQuantity = 20
                        }),

                var type when
                    type == typeof(InventoryItemResponse) =>
                    ToNode(
                        new InventoryItemResponse
                        {
                            Id = 25,
                            Name = "Air Filter",
                            Sku = "FILTER-100",
                            QuantityOnHand = 8,
                            ReorderThreshold = 10,
                            ReorderQuantity = 20,
                            Status = "ReorderPending",
                            CreatedAt =
                                new DateTime(
                                    2026,
                                    8,
                                    4,
                                    12,
                                    0,
                                    0,
                                    DateTimeKind.Utc),
                            UpdatedAt =
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
                    type == typeof(ReorderEventResponse) =>
                    ToNode(
                        new ReorderEventResponse
                        {
                            Id = 1001,
                            InventoryItemId = 25,
                            QuantityAtTrigger = 8,
                            RequestedQuantity = 20,
                            TriggeredAt =
                                new DateTime(
                                    2026,
                                    8,
                                    4,
                                    12,
                                    30,
                                    0,
                                    DateTimeKind.Utc),
                            Status = "SupplierAccepted",
                            SupplierOrderId =
                                Guid.Parse(
                                    "8d7c6d20-91be-4d08-a104-b831a2295b84"),
                            SupplierOrderStatus = "Accepted",
                            SupplierAcceptedAtUtc =
                                new DateTime(
                                    2026,
                                    8,
                                    4,
                                    12,
                                    30,
                                    2,
                                    DateTimeKind.Utc),
                            SupplierRejectionReason = null
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