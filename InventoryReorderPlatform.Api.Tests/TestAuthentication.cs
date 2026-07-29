using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InventoryReorderPlatform.Api.DTOs;
using InventoryReorderPlatform.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryReorderPlatform.Api.Tests;

internal static class TestAuthentication
{
    private const string Password =
        "Test-User-Password1!";

    public static async Task<(
        HttpClient Client,
        string Email)> CreateAuthenticatedClientAsync(
            InventoryApiFactory factory,
            string role,
            CancellationToken cancellationToken)
    {
        var client = factory.CreateClient();

        var email =
            $"{role.ToLowerInvariant()}-" +
            $"{Guid.NewGuid():N}@test.local";

        using (var scope =
               factory.Services.CreateScope())
        {
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

            AssertSucceeded(
                createResult,
                "creating the test user");

            var roleResult =
                await userManager.AddToRoleAsync(
                    user,
                    role);

            AssertSucceeded(
                roleResult,
                "assigning the test role");
        }

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

        return (client, email);
    }

    private static void AssertSucceeded(
        IdentityResult result,
        string operation)
    {
        Assert.True(
            result.Succeeded,
            $"{operation}: " +
            string.Join(
                "; ",
                result.Errors.Select(
                    error =>
                        $"{error.Code}: " +
                        error.Description)));
    }
}