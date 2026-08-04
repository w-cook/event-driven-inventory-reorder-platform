using InventoryReorderPlatform.Api.DTOs;
using InventoryReorderPlatform.Api.Services;
using InventoryReorderPlatform.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InventoryReorderPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EndpointSummary("Log in")]
    [EndpointDescription(
        "Authenticates an active user account and returns a JWT bearer token " +
        "with the user's assigned roles.")]
    [Consumes("application/json")]
    [ProducesResponseType<LoginResponse>(
        StatusCodes.Status200OK,
        "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest,
        "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status401Unauthorized,
        "application/problem+json")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request)
    {
        var email = request.Email.Trim();

        var user =
            await _userManager.FindByEmailAsync(email);

        if (user is null || !user.IsActive)
        {
            _logger.LogWarning(
                "Failed login attempt for {Email}.",
                email);

            return InvalidCredentials();
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning(
                "Login rejected for locked account {UserId}.",
                user.Id);

            return InvalidCredentials();
        }

        var passwordIsValid =
            await _userManager.CheckPasswordAsync(
                user,
                request.Password);

        if (!passwordIsValid)
        {
            if (await _userManager.GetLockoutEnabledAsync(user))
            {
                var accessFailedResult =
                    await _userManager.AccessFailedAsync(user);

                if (!accessFailedResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Failed to update the account access-failure count.");
                }
            }

            _logger.LogWarning(
                "Failed login attempt for account {UserId}.",
                user.Id);

            return InvalidCredentials();
        }

        if (await _userManager.GetAccessFailedCountAsync(user) > 0)
        {
            var resetResult =
                await _userManager
                    .ResetAccessFailedCountAsync(user);

            if (!resetResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to reset the account access-failure count.");
            }
        }

        var token =
            await _jwtTokenService.CreateAsync(user);

        var roles =
            await _userManager.GetRolesAsync(user);

        _logger.LogInformation(
            "User {UserId} logged in successfully.",
            user.Id);

        return Ok(
            new LoginResponse(
                token.AccessToken,
                token.ExpiresAtUtc,
                user.Id,
                user.Email ?? email,
                roles.ToArray()));
    }

    private UnauthorizedObjectResult InvalidCredentials()
    {
        return Unauthorized(
            new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Login failed.",
                Detail = "Invalid email or password."
            });
    }
}