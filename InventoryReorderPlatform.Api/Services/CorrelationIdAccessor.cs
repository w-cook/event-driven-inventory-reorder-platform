using InventoryReorderPlatform.Api.Middleware;

namespace InventoryReorderPlatform.Api.Services;

public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdAccessor(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCorrelationId()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext?.Items[
                CorrelationIdMiddleware.ItemKey] is string correlationId &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        return Guid.NewGuid().ToString("N");
    }
}