using InventoryReorderPlatform.SupplierMockApi.Data;
using InventoryReorderPlatform.SupplierMockApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryReorderPlatform.SupplierMockApi.Tests;

public sealed class SupplierDbContextTests
    : IClassFixture<SupplierApiFactory>
{
    private readonly SupplierApiFactory _factory;

    public SupplierDbContextTests(
        SupplierApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SupplierOrders_RejectDuplicateIdempotencyKey()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var idempotencyKey =
            $"database-test-{Guid.NewGuid():N}";

        await AddSupplierOrderAsync(
            idempotencyKey,
            reorderEventId: 2001,
            cancellationToken);

        var exception =
            await Assert.ThrowsAsync<DbUpdateException>(
                async () =>
                    await AddSupplierOrderAsync(
                        idempotencyKey,
                        reorderEventId: 2002,
                        cancellationToken));

        Assert.NotNull(exception);
    }

    private async Task AddSupplierOrderAsync(
        string idempotencyKey,
        int reorderEventId,
        CancellationToken cancellationToken)
    {
        using var scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<SupplierDbContext>();

        dbContext.SupplierOrders.Add(
            new SupplierOrder
            {
                Id = Guid.NewGuid(),
                IdempotencyKey = idempotencyKey,
                ReorderEventId = reorderEventId,
                InventoryItemId = 25,
                Sku = "WIDGET-100",
                RequestedQuantity = 40,
                TriggeredAtUtc = new DateTime(
                    2026,
                    8,
                    2,
                    13,
                    30,
                    0,
                    DateTimeKind.Utc),
                Status = SupplierOrderStatuses.Accepted,
                AcceptedAtUtc = DateTime.UtcNow
            });

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }
}