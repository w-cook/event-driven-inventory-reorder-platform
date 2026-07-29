using System.Net;
using System.Net.Http.Json;
using InventoryReorderPlatform.Api.DTOs;
using InventoryReorderPlatform.Api.Security;
using InventoryReorderPlatform.Api.Services;
using InventoryReorderPlatform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryReorderPlatform.Api.Tests;

public sealed class AuthorizationTests
    : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public AuthorizationTests(
        InventoryApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousRequest_IsRejected()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            "/api/inventoryitems",
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Viewer_CanReadInventory()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Demo-User",
            "viewer");

        var response = await client.GetAsync(
            "/api/inventoryitems",
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task Viewer_CannotUpdateInventoryQuantity()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Demo-User",
            "viewer");

        var request = new
        {
            Name = "Authorization Test Item",
            Sku = "AUTH-VIEWER-001",
            QuantityOnHand = 20,
            ReorderThreshold = 5
        };

        var response = await client.PutAsJsonAsync(
            "/api/inventoryitems/999",
            request,
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_RejectsOperator()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Demo-User",
            "operator");

        var response = await client.GetAsync(
            "/api/audit-records",
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateQuantity_AsOperator_CreatesAuditRecord()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Demo-User",
            "operator");

        var createRequest = new
        {
            Name = "Operator Test Item",
            Sku = "AUTH-OPERATOR-001",
            QuantityOnHand = 20,
            ReorderThreshold = 5
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/inventoryitems",
            createRequest,
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var createdItem =
            await createResponse.Content
                .ReadFromJsonAsync<InventoryItemResponse>(
                    cancellationToken: cancellationToken);

        Assert.NotNull(createdItem);

        var updateRequest = new
        {
            Name = createdItem.Name,
            Sku = createdItem.Sku,
            QuantityOnHand = 25,
            ReorderThreshold = createdItem.ReorderThreshold
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/inventoryitems/{createdItem.Id}",
            updateRequest,
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            updateResponse.StatusCode);

        var updatedItem =
            await updateResponse.Content
                .ReadFromJsonAsync<InventoryItemResponse>(
                    cancellationToken: cancellationToken);

        Assert.NotNull(updatedItem);
        Assert.Equal(25, updatedItem.QuantityOnHand);

        using var scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var auditRecord =
            await dbContext.AuditRecords
                .AsNoTracking()
                .SingleAsync(
                    record =>
                        record.Action ==
                            AuditActions.InventoryItemUpdated
                        && record.EntityId ==
                            createdItem.Id.ToString(),
                    cancellationToken);

        Assert.Equal(
            "operator@example.local",
            auditRecord.UserName);

        Assert.Equal(
            AppRoles.Operator,
            auditRecord.Role);

        Assert.Equal(
            AuditActions.InventoryItemUpdated,
            auditRecord.Action);

        Assert.Equal(
            "InventoryItem",
            auditRecord.EntityType);

        Assert.Equal(
            createdItem.Id.ToString(),
            auditRecord.EntityId);

        Assert.False(
            string.IsNullOrWhiteSpace(
                auditRecord.Details));

        Assert.Contains(
            "\"QuantityOnHand\":25",
            auditRecord.Details);

        Assert.NotEqual(
            default,
            auditRecord.OccurredAt);
    }
}