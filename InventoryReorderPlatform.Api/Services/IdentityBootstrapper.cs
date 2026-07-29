using InventoryReorderPlatform.Api.Security;
using InventoryReorderPlatform.Data.Models;
using Microsoft.AspNetCore.Identity;

namespace InventoryReorderPlatform.Api.Services;

public sealed class IdentityBootstrapper
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentityBootstrapper> _logger;

    public IdentityBootstrapper(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<IdentityBootstrapper> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await EnsureRoleExistsAsync(AppRoles.Viewer);
        await EnsureRoleExistsAsync(AppRoles.Operator);
        await EnsureRoleExistsAsync(AppRoles.Administrator);

        var email =
            _configuration["BootstrapAdmin:Email"]?.Trim();

        var password =
            _configuration["BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) &&
            string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "No bootstrap Administrator credentials were configured.");

            return;
        }

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "BootstrapAdmin:Email and BootstrapAdmin:Password " +
                "must either both be configured or both be omitted.");
        }

        var administrator =
            await _userManager.FindByEmailAsync(email);

        if (administrator is null)
        {
            administrator = new ApplicationUser
            {
                UserName = email,
                Email = email,
                IsActive = true,
                LockoutEnabled = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var createResult =
                await _userManager.CreateAsync(
                    administrator,
                    password);

            EnsureSucceeded(
                createResult,
                "creating the bootstrap Administrator");

            _logger.LogInformation(
                "Created bootstrap Administrator {UserId}.",
                administrator.Id);
        }

        if (!administrator.IsActive)
        {
            _logger.LogWarning(
                "The configured bootstrap Administrator {UserId} " +
                "exists but is inactive.",
                administrator.Id);

            return;
        }

        if (!await _userManager.IsInRoleAsync(
                administrator,
                AppRoles.Administrator))
        {
            var roleResult =
                await _userManager.AddToRoleAsync(
                    administrator,
                    AppRoles.Administrator);

            EnsureSucceeded(
                roleResult,
                "assigning the Administrator role");

            _logger.LogInformation(
                "Assigned the Administrator role to user {UserId}.",
                administrator.Id);
        }
    }

    private async Task EnsureRoleExistsAsync(
        string roleName)
    {
        if (await _roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result =
            await _roleManager.CreateAsync(
                new IdentityRole(roleName));

        EnsureSucceeded(
            result,
            $"creating the {roleName} role");
    }

    private static void EnsureSucceeded(
        IdentityResult result,
        string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(
            "; ",
            result.Errors.Select(
                error =>
                    $"{error.Code}: {error.Description}"));

        throw new InvalidOperationException(
            $"Identity operation failed while {operation}: {errors}");
    }
}