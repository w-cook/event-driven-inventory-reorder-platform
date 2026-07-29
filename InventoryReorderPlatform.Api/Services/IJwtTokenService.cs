using InventoryReorderPlatform.Api.Security;
using InventoryReorderPlatform.Data.Models;

namespace InventoryReorderPlatform.Api.Services;

public interface IJwtTokenService
{
    Task<AccessTokenResult> CreateAsync(
        ApplicationUser user);
}