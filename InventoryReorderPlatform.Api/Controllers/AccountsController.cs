using InventoryReorderPlatform.Api.DTOs;
using InventoryReorderPlatform.Api.Security;
using InventoryReorderPlatform.Api.Services;
using InventoryReorderPlatform.Data;
using InventoryReorderPlatform.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InventoryReorderPlatform.Api.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize(Policy = AppPolicies.AdminOnly)]
public sealed class AccountsController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _dbContext;
    private readonly IAuditService _auditService;

    public AccountsController(
        UserManager<ApplicationUser> userManager,
        AppDbContext dbContext,
        IAuditService auditService)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _auditService = auditService;
    }

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> Create(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        var role =
            AppRoles.Normalize(request.Role);

        if (role is null)
        {
            ModelState.AddModelError(
                nameof(request.Role),
                $"Role must be one of: " +
                $"{string.Join(", ", AppRoles.All)}.");

            return ValidationProblem(ModelState);
        }

        var existingUser =
            await _userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            return Conflict(
                new ProblemDetails
                {
                    Status =
                        StatusCodes.Status409Conflict,

                    Title =
                        "Account already exists.",

                    Detail =
                        "An account with that email address already exists."
                });
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            IsActive = true,
            LockoutEnabled = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult =
            await _userManager.CreateAsync(
                user,
                request.Password);

        if (!createResult.Succeeded)
        {
            return IdentityValidationProblem(
                createResult);
        }

        var roleResult =
            await _userManager.AddToRoleAsync(
                user,
                role);

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);

            return IdentityValidationProblem(
                roleResult);
        }

        await _auditService.AddRecordAsync(
            User,
            AuditActions.UserAccountCreated,
            nameof(ApplicationUser),
            user.Id,
            new
            {
                user.Email,
                Role = role,
                user.IsActive
            },
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        var response = new AccountResponse(
            user.Id,
            user.Email ?? email,
            [role],
            user.IsActive,
            user.CreatedAtUtc);

        return Created(
            $"/api/accounts/{user.Id}",
            response);
    }

    [HttpPatch("{id}/role")]
    public async Task<ActionResult<AccountResponse>> UpdateRole(
        [FromRoute] string id,
        UpdateAccountRoleRequest request,
        CancellationToken cancellationToken)
    {
        var requestedRole =
            AppRoles.Normalize(request.Role);

        if (requestedRole is null)
        {
            ModelState.AddModelError(
                nameof(request.Role),
                $"Role must be one of: " +
                $"{string.Join(", ", AppRoles.All)}.");

            return ValidationProblem(ModelState);
        }

        var user =
            await _userManager.FindByIdAsync(id);

        if (user is null)
        {
            return NotFound(
                new ProblemDetails
                {
                    Status =
                        StatusCodes.Status404NotFound,

                    Title =
                        "Account not found.",

                    Detail =
                        $"Account '{id}' does not exist."
                });
        }

        var currentRoles =
            await _userManager.GetRolesAsync(user);

        if (currentRoles.Count == 1 &&
            string.Equals(
                currentRoles[0],
                requestedRole,
                StringComparison.Ordinal))
        {
            return Ok(
                new AccountResponse(
                    user.Id,
                    user.Email ?? user.UserName ?? user.Id,
                    currentRoles.ToArray(),
                    user.IsActive,
                    user.CreatedAtUtc));
        }

        var isCurrentAdministrator =
            currentRoles.Contains(
                AppRoles.Administrator,
                StringComparer.Ordinal);

        var isRemovingAdministrator =
            isCurrentAdministrator &&
            !string.Equals(
                requestedRole,
                AppRoles.Administrator,
                StringComparison.Ordinal);

        if (isRemovingAdministrator &&
            user.IsActive)
        {
            var administrators =
                await _userManager.GetUsersInRoleAsync(
                    AppRoles.Administrator);

            var activeAdministratorCount =
                administrators.Count(
                    administrator =>
                        administrator.IsActive);

            if (activeAdministratorCount <= 1)
            {
                return Conflict(
                    new ProblemDetails
                    {
                        Status =
                            StatusCodes.Status409Conflict,

                        Title =
                            "Final Administrator protected.",

                        Detail =
                            "The final active Administrator " +
                            "cannot be assigned another role."
                    });
            }
        }

        var rolesToRemove =
            currentRoles
                .Where(
                    role =>
                        !string.Equals(
                            role,
                            requestedRole,
                            StringComparison.Ordinal))
                .ToArray();

        var addedRequestedRole = false;

        if (!currentRoles.Contains(
                requestedRole,
                StringComparer.Ordinal))
        {
            var addRoleResult =
                await _userManager.AddToRoleAsync(
                    user,
                    requestedRole);

            if (!addRoleResult.Succeeded)
            {
                return IdentityValidationProblem(
                    addRoleResult);
            }

            addedRequestedRole = true;
        }

        if (rolesToRemove.Length > 0)
        {
            var removeRolesResult =
                await _userManager.RemoveFromRolesAsync(
                    user,
                    rolesToRemove);

            if (!removeRolesResult.Succeeded)
            {
                if (addedRequestedRole)
                {
                    await _userManager.RemoveFromRoleAsync(
                        user,
                        requestedRole);
                }

                return IdentityValidationProblem(
                    removeRolesResult);
            }
        }

        var securityStampResult =
            await _userManager
                .UpdateSecurityStampAsync(user);

        if (!securityStampResult.Succeeded)
        {
            return IdentityValidationProblem(
                securityStampResult);
        }

        var updatedRoles =
            await _userManager.GetRolesAsync(user);

        await _auditService.AddRecordAsync(
            User,
            AuditActions.UserAccountRoleChanged,
            nameof(ApplicationUser),
            user.Id,
            new
            {
                user.Email,
                PreviousRoles = currentRoles,
                CurrentRoles = updatedRoles
            },
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Ok(
            new AccountResponse(
                user.Id,
                user.Email ?? user.UserName ?? user.Id,
                updatedRoles.ToArray(),
                user.IsActive,
                user.CreatedAtUtc));
    }

    [HttpPatch("{id}/status")]
    public async Task<ActionResult<AccountResponse>> UpdateStatus(
    [FromRoute] string id,
    UpdateAccountStatusRequest request,
    CancellationToken cancellationToken)
    {
        var user =
            await _userManager.FindByIdAsync(id);

        if (user is null)
        {
            return NotFound(
                new ProblemDetails
                {
                    Status =
                        StatusCodes.Status404NotFound,

                    Title =
                        "Account not found.",

                    Detail =
                        $"Account '{id}' does not exist."
                });
        }

        var currentRoles =
            await _userManager.GetRolesAsync(user);

        if (user.IsActive == request.IsActive)
        {
            return Ok(
                new AccountResponse(
                    user.Id,
                    user.Email ?? user.UserName ?? user.Id,
                    currentRoles.ToArray(),
                    user.IsActive,
                    user.CreatedAtUtc));
        }

        var isAdministrator =
            currentRoles.Contains(
                AppRoles.Administrator,
                StringComparer.Ordinal);

        var isDeactivatingAdministrator =
            user.IsActive &&
            !request.IsActive &&
            isAdministrator;

        if (isDeactivatingAdministrator)
        {
            var administrators =
                await _userManager.GetUsersInRoleAsync(
                    AppRoles.Administrator);

            var activeAdministratorCount =
                administrators.Count(
                    administrator =>
                        administrator.IsActive);

            if (activeAdministratorCount <= 1)
            {
                return Conflict(
                    new ProblemDetails
                    {
                        Status =
                            StatusCodes.Status409Conflict,

                        Title =
                            "Final Administrator protected.",

                        Detail =
                            "The final active Administrator " +
                            "cannot be deactivated."
                    });
            }
        }

        var previousIsActive =
            user.IsActive;

        // Revoke all currently issued JWTs before changing
        // the account's active state.
        var securityStampResult =
            await _userManager
                .UpdateSecurityStampAsync(user);

        if (!securityStampResult.Succeeded)
        {
            return IdentityValidationProblem(
                securityStampResult);
        }

        user.IsActive =
            request.IsActive;

        var updateResult =
            await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return IdentityValidationProblem(
                updateResult);
        }

        await _auditService.AddRecordAsync(
            User,
            AuditActions.UserAccountStatusChanged,
            nameof(ApplicationUser),
            user.Id,
            new
            {
                user.Email,
                PreviousIsActive =
                    previousIsActive,
                CurrentIsActive =
                    user.IsActive,
                Roles =
                    currentRoles
            },
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Ok(
            new AccountResponse(
                user.Id,
                user.Email ?? user.UserName ?? user.Id,
                currentRoles.ToArray(),
                user.IsActive,
                user.CreatedAtUtc));
    }

    private BadRequestObjectResult
        IdentityValidationProblem(
            IdentityResult result)
    {
        var errors = result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(error => error.Description)
                    .ToArray());

        return BadRequest(
            new ValidationProblemDetails(errors)
            {
                Status =
                    StatusCodes.Status400BadRequest,

                Title =
                    "Account validation failed."
            });
    }
}