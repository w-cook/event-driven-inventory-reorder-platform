using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InventoryReorderPlatform.Api.DTOs;
using InventoryReorderPlatform.Api.Security;
using InventoryReorderPlatform.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryReorderPlatform.Api.Tests;

public sealed class AuthenticationTests
    : IClassFixture<InventoryApiFactory>
{
    private const string Password =
        "Test-Login-Password1!";

    private readonly InventoryApiFactory _factory;

    public AuthenticationTests(
        InventoryApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsJwtAndRoles()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();

        var email =
            $"operator-{Guid.NewGuid():N}@test.local";

        await CreateUserAsync(
            email,
            AppRoles.Operator);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = email,
                Password = Password
            },
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var result =
            await response.Content
                .ReadFromJsonAsync<LoginResponse>(
                    cancellationToken: cancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.Equal(email, result.Email);
        Assert.Contains(AppRoles.Operator, result.Roles);
        Assert.True(result.ExpiresAtUtc > DateTime.UtcNow);

        var tokenHandler =
            new JwtSecurityTokenHandler();

        var token =
            tokenHandler.ReadJwtToken(
                result.AccessToken);

        Assert.Equal(
            "InventoryReorderPlatform.Api.Tests",
            token.Issuer);

        Assert.Contains(
            "InventoryReorderPlatform.Api.Tests.Client",
            token.Audiences);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();

        var email =
            $"viewer-{Guid.NewGuid():N}@test.local";

        await CreateUserAsync(
            email,
            AppRoles.Viewer);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = email,
                Password = "Incorrect-Password1!"
            },
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    private async Task CreateUserAsync(
        string email,
        string role)
    {
        using var scope =
            _factory.Services.CreateScope();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<
                    UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            IsActive = true,
            LockoutEnabled = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult =
            await userManager.CreateAsync(
                user,
                Password);

        Assert.True(
            createResult.Succeeded,
            string.Join(
                "; ",
                createResult.Errors.Select(
                    error => error.Description)));

        var roleResult =
            await userManager.AddToRoleAsync(
                user,
                role);

        Assert.True(
            roleResult.Succeeded,
            string.Join(
                "; ",
                roleResult.Errors.Select(
                    error => error.Description)));
    }

    [Fact]
    public async Task JwtViewer_CanReadProtectedInventory()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();

        var email =
            $"jwt-viewer-{Guid.NewGuid():N}@test.local";

        await CreateUserAsync(
            email,
            AppRoles.Viewer);

        var loginResponse =
            await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest
                {
                    Email = email,
                    Password = Password
                },
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode);

        var login =
            await loginResponse.Content
                .ReadFromJsonAsync<LoginResponse>(
                    cancellationToken:
                        cancellationToken);

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        var protectedResponse =
            await client.GetAsync(
                "/api/inventoryitems",
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            protectedResponse.StatusCode);
    }

    [Fact]
    public async Task Jwt_IsRejected_WhenAccountBecomesInactive()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var authenticated =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Viewer,
                    cancellationToken);

        using var client = authenticated.Client;

        using (var scope =
               _factory.Services.CreateScope())
        {
            var userManager =
                scope.ServiceProvider
                    .GetRequiredService<
                        UserManager<ApplicationUser>>();

            var user =
                await userManager.FindByEmailAsync(
                    authenticated.Email);

            Assert.NotNull(user);

            user.IsActive = false;

            var updateResult =
                await userManager.UpdateAsync(user);

            Assert.True(
                updateResult.Succeeded,
                string.Join(
                    "; ",
                    updateResult.Errors.Select(
                        error => error.Description)));
        }

        var response = await client.GetAsync(
            "/api/inventoryitems",
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Jwt_IsRejected_WhenSecurityStampChanges()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var authenticated =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Operator,
                    cancellationToken);

        using var client = authenticated.Client;

        using (var scope =
               _factory.Services.CreateScope())
        {
            var userManager =
                scope.ServiceProvider
                    .GetRequiredService<
                        UserManager<ApplicationUser>>();

            var user =
                await userManager.FindByEmailAsync(
                    authenticated.Email);

            Assert.NotNull(user);

            var updateResult =
                await userManager
                    .UpdateSecurityStampAsync(user);

            Assert.True(
                updateResult.Succeeded,
                string.Join(
                    "; ",
                    updateResult.Errors.Select(
                        error => error.Description)));
        }

        var response = await client.GetAsync(
            "/api/inventoryitems",
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }
}