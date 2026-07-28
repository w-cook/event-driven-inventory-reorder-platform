using System.Net;
using InventoryReorderPlatform.Api.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace InventoryReorderPlatform.Api.Tests;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task RequestWithoutCorrelationId_ReturnsGeneratedCorrelationId()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        await using var app =
            await CreateTestApplicationAsync(cancellationToken);

        using var client = app.GetTestClient();

        var response = await client.GetAsync(
            "/test",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(
            response.Headers.TryGetValues(
                CorrelationIdMiddleware.HeaderName,
                out var values));

        var correlationId = Assert.Single(values);

        Assert.False(string.IsNullOrWhiteSpace(correlationId));

        Assert.True(
            Guid.TryParseExact(
                correlationId,
                "N",
                out _));
    }

    [Fact]
    public async Task RequestWithCorrelationId_ReturnsSameCorrelationId()
    {
        const string expectedCorrelationId =
            "day141-observability-test";

        var cancellationToken =
            TestContext.Current.CancellationToken;

        await using var app =
            await CreateTestApplicationAsync(cancellationToken);

        using var client = app.GetTestClient();

        using var request =
            new HttpRequestMessage(HttpMethod.Get, "/test");

        request.Headers.Add(
            CorrelationIdMiddleware.HeaderName,
            expectedCorrelationId);

        var response = await client.SendAsync(
            request,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(
            response.Headers.TryGetValues(
                CorrelationIdMiddleware.HeaderName,
                out var values));

        var returnedCorrelationId = Assert.Single(values);

        Assert.Equal(
            expectedCorrelationId,
            returnedCorrelationId);
    }

    private static async Task<WebApplication>
        CreateTestApplicationAsync(
            CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

        builder.WebHost.UseTestServer();

        var app = builder.Build();

        app.UseMiddleware<CorrelationIdMiddleware>();

        app.MapGet(
            "/test",
            () => Results.Ok());

        await app.StartAsync(cancellationToken);

        return app;
    }
}