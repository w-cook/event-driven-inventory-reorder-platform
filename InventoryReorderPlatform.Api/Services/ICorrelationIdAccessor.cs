namespace InventoryReorderPlatform.Api.Services;

public interface ICorrelationIdAccessor
{
    string GetCorrelationId();
}