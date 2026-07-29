using System.ComponentModel.DataAnnotations;

namespace InventoryReorderPlatform.Api.DTOs;

public sealed class CreateAccountRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}