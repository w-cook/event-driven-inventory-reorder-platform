using Microsoft.AspNetCore.Identity;

namespace InventoryReorderPlatform.Data.Models;

public sealed class ApplicationUser : IdentityUser
{
    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
}