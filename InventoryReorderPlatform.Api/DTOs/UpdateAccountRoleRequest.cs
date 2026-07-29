using System.ComponentModel.DataAnnotations;

namespace InventoryReorderPlatform.Api.DTOs;

public sealed class UpdateAccountRoleRequest
{
    [Required]
    public string Role { get; set; } = string.Empty;
}