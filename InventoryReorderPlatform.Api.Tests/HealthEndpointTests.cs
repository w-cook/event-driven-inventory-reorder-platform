using System.Net;
using System.Net.Http.Json;
using InventoryReorderPlatform.Api.DTOs;
using InventoryReorderPlatform.Api.Security;

namespace InventoryReorderPlatform.Api.Tests;

public sealed class HealthEndpointTests
    : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public HealthEndpointTests(
        InventoryApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AlivenessEndpoint_ReturnsOk()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            "/alive",
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            "/health",
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task OperationsHealth_ReturnsHealthy_WhenDatabaseIsAvailable()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var authenticated =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Viewer,
                    cancellationToken);

        using var client = authenticated.Client;

        var response = await client.GetAsync(
            "/api/operations/health",
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var health =
            await response.Content
                .ReadFromJsonAsync<OperationsHealthResponse>(
                    cancellationToken: cancellationToken);

        Assert.NotNull(health);

        Assert.Equal(
            "Healthy",
            health.Status);

        Assert.Equal(
            "Connected",
            health.DatabaseStatus);

        Assert.NotNull(
            health.InventoryItemCount);

        Assert.NotNull(
            health.ReorderEventCount);

        Assert.True(
            health.InventoryItemCount >= 0);

        Assert.True(
            health.ReorderEventCount >= 0);

        Assert.NotEqual(
            default,
            health.CheckedAt);
    }
}