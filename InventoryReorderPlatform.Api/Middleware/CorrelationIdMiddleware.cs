using System.Diagnostics;

namespace InventoryReorderPlatform.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        Activity.Current?.SetTag(
            "correlation.id",
            correlationId);

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(
                HeaderName,
                out var headerValues))
        {
            var suppliedCorrelationId = headerValues
                .FirstOrDefault()
                ?.Trim();

            if (!string.IsNullOrWhiteSpace(suppliedCorrelationId))
            {
                return suppliedCorrelationId;
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}