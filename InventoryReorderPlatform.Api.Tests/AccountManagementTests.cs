using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InventoryReorderPlatform.Api.DTOs;
using InventoryReorderPlatform.Api.Security;
using InventoryReorderPlatform.Api.Services;
using InventoryReorderPlatform.Data;
using InventoryReorderPlatform.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryReorderPlatform.Api.Tests;

public sealed class AccountManagementTests
    : IClassFixture<InventoryApiFactory>
{
    private const string ValidPassword =
        "New-Account-Password1!";

    private readonly InventoryApiFactory _factory;

    public AccountManagementTests(
        InventoryApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateAccount_AsOperator_ReturnsForbidden()
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

        var response = await client.PostAsJsonAsync(
            "/api/accounts",
            new CreateAccountRequest
            {
                Email =
                    $"forbidden-{Guid.NewGuid():N}@test.local",
                Password = ValidPassword,
                Role = AppRoles.Viewer
            },
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateAccount_AsAdministrator_CreatesUserRoleAndAuditRecord()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var authenticated =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Administrator,
                    cancellationToken);

        using var client = authenticated.Client;

        var targetEmail =
            $"viewer-{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync(
            "/api/accounts",
            new CreateAccountRequest
            {
                Email = targetEmail,
                Password = ValidPassword,
                Role = AppRoles.Viewer
            },
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var account =
            await response.Content
                .ReadFromJsonAsync<AccountResponse>(
                    cancellationToken:
                        cancellationToken);

        Assert.NotNull(account);
        Assert.Equal(targetEmail, account.Email);
        Assert.True(account.IsActive);
        Assert.NotEqual(default, account.CreatedAtUtc);

        var returnedRole =
            Assert.Single(account.Roles);

        Assert.Equal(
            AppRoles.Viewer,
            returnedRole);

        using var scope =
            _factory.Services.CreateScope();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<
                    UserManager<ApplicationUser>>();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var createdUser =
            await userManager.FindByEmailAsync(
                targetEmail);

        Assert.NotNull(createdUser);
        Assert.Equal(account.Id, createdUser.Id);
        Assert.True(createdUser.IsActive);

        var roles =
            await userManager.GetRolesAsync(
                createdUser);

        var persistedRole =
            Assert.Single(roles);

        Assert.Equal(
            AppRoles.Viewer,
            persistedRole);

        var auditRecord =
            await dbContext.AuditRecords
                .AsNoTracking()
                .SingleAsync(
                    record =>
                        record.Action ==
                            AuditActions.UserAccountCreated
                        && record.EntityId ==
                            createdUser.Id,
                    cancellationToken);

        Assert.Equal(
            authenticated.Email,
            auditRecord.UserName);

        Assert.Equal(
            AppRoles.Administrator,
            auditRecord.Role);

        Assert.Equal(
            nameof(ApplicationUser),
            auditRecord.EntityType);

        var details =
            Assert.IsType<string>(
                auditRecord.Details);

        Assert.Contains(
            targetEmail,
            details);

        Assert.Contains(
            AppRoles.Viewer,
            details);
    }

    [Fact]
    public async Task CreateAccount_WithDuplicateEmail_ReturnsConflict()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var authenticated =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Administrator,
                    cancellationToken);

        using var client = authenticated.Client;

        var email =
            $"duplicate-{Guid.NewGuid():N}@test.local";

        var request = new CreateAccountRequest
        {
            Email = email,
            Password = ValidPassword,
            Role = AppRoles.Operator
        };

        var firstResponse =
            await client.PostAsJsonAsync(
                "/api/accounts",
                request,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        var duplicateResponse =
            await client.PostAsJsonAsync(
                "/api/accounts",
                request,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Conflict,
            duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task CreateAccount_WithInvalidRole_ReturnsBadRequest()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var authenticated =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Administrator,
                    cancellationToken);

        using var client = authenticated.Client;

        var email =
            $"invalid-role-{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync(
            "/api/accounts",
            new CreateAccountRequest
            {
                Email = email,
                Password = ValidPassword,
                Role = "SuperUser"
            },
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        using var scope =
            _factory.Services.CreateScope();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<
                    UserManager<ApplicationUser>>();

        Assert.Null(
            await userManager.FindByEmailAsync(email));
    }

    [Fact]
    public async Task CreateAccount_WithWeakPassword_ReturnsBadRequest()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var authenticated =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Administrator,
                    cancellationToken);

        using var client = authenticated.Client;

        var email =
            $"weak-password-{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync(
            "/api/accounts",
            new CreateAccountRequest
            {
                Email = email,
                Password = "NoDigitsHere!",
                Role = AppRoles.Viewer
            },
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        var responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        Assert.Contains(
            "PasswordRequiresDigit",
            responseBody);

        using var scope =
            _factory.Services.CreateScope();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<
                    UserManager<ApplicationUser>>();

        Assert.Null(
            await userManager.FindByEmailAsync(email));
    }

    [Fact]
    public async Task UpdateRole_AsAdministrator_ChangesRoleAuditsAndRevokesExistingJwt()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var target =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Operator,
                    cancellationToken);

        using var targetClient = target.Client;

        var administrator =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Administrator,
                    cancellationToken);

        using var administratorClient =
            administrator.Client;

        string targetUserId;

        using (var scope =
               _factory.Services.CreateScope())
        {
            var userManager =
                scope.ServiceProvider
                    .GetRequiredService<
                        UserManager<ApplicationUser>>();

            var targetUser =
                await userManager.FindByEmailAsync(
                    target.Email);

            Assert.NotNull(targetUser);

            targetUserId = targetUser.Id;
        }

        var response =
            await administratorClient.PatchAsJsonAsync(
                $"/api/accounts/{targetUserId}/role",
                new UpdateAccountRoleRequest
                {
                    Role = AppRoles.Viewer
                },
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var account =
            await response.Content
                .ReadFromJsonAsync<AccountResponse>(
                    cancellationToken:
                        cancellationToken);

        Assert.NotNull(account);
        Assert.Equal(targetUserId, account.Id);
        Assert.Equal(target.Email, account.Email);

        var returnedRole =
            Assert.Single(account.Roles);

        Assert.Equal(
            AppRoles.Viewer,
            returnedRole);

        using (var scope =
               _factory.Services.CreateScope())
        {
            var userManager =
                scope.ServiceProvider
                    .GetRequiredService<
                        UserManager<ApplicationUser>>();

            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

            var targetUser =
                await userManager.FindByIdAsync(
                    targetUserId);

            Assert.NotNull(targetUser);

            var roles =
                await userManager.GetRolesAsync(
                    targetUser);

            var persistedRole =
                Assert.Single(roles);

            Assert.Equal(
                AppRoles.Viewer,
                persistedRole);

            var auditRecord =
                await dbContext.AuditRecords
                    .AsNoTracking()
                    .SingleAsync(
                        record =>
                            record.Action ==
                                AuditActions
                                    .UserAccountRoleChanged
                            && record.EntityId ==
                                targetUserId,
                        cancellationToken);

            Assert.Equal(
                administrator.Email,
                auditRecord.UserName);

            Assert.Equal(
                AppRoles.Administrator,
                auditRecord.Role);

            Assert.Equal(
                nameof(ApplicationUser),
                auditRecord.EntityType);

            Assert.NotNull(auditRecord.Details);

            Assert.Contains(
                AppRoles.Operator,
                auditRecord.Details);

            Assert.Contains(
                AppRoles.Viewer,
                auditRecord.Details);
        }

        var oldTokenResponse =
            await targetClient.PostAsJsonAsync(
                "/api/inventoryitems",
                new
                {
                    Name = "Revoked Token Test Item",
                    Sku =
                        $"REVOKED-{Guid.NewGuid():N}",
                    QuantityOnHand = 20,
                    ReorderThreshold = 5
                },
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            oldTokenResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_AsOperator_ReturnsForbidden()
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

        var response =
            await client.PatchAsJsonAsync(
                $"/api/accounts/{Guid.NewGuid()}/role",
                new UpdateAccountRoleRequest
                {
                    Role = AppRoles.Viewer
                },
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_WithInvalidRole_ReturnsBadRequest()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var target =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Viewer,
                    cancellationToken);

        using var targetClient = target.Client;

        var administrator =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Administrator,
                    cancellationToken);

        using var administratorClient =
            administrator.Client;

        string targetUserId;

        using (var scope =
               _factory.Services.CreateScope())
        {
            var userManager =
                scope.ServiceProvider
                    .GetRequiredService<
                        UserManager<ApplicationUser>>();

            var targetUser =
                await userManager.FindByEmailAsync(
                    target.Email);

            Assert.NotNull(targetUser);

            targetUserId = targetUser.Id;
        }

        var response =
            await administratorClient.PatchAsJsonAsync(
                $"/api/accounts/{targetUserId}/role",
                new UpdateAccountRoleRequest
                {
                    Role = "SuperUser"
                },
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        using var verificationScope =
            _factory.Services.CreateScope();

        var verificationUserManager =
            verificationScope.ServiceProvider
                .GetRequiredService<
                    UserManager<ApplicationUser>>();

        var unchangedUser =
            await verificationUserManager.FindByIdAsync(
                targetUserId);

        Assert.NotNull(unchangedUser);

        var roles =
            await verificationUserManager.GetRolesAsync(
                unchangedUser);

        var unchangedRole =
            Assert.Single(roles);

        Assert.Equal(
            AppRoles.Viewer,
            unchangedRole);
    }

    [Fact]
    public async Task UpdateRole_ForMissingAccount_ReturnsNotFound()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var administrator =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Administrator,
                    cancellationToken);

        using var client =
            administrator.Client;

        var response =
            await client.PatchAsJsonAsync(
                $"/api/accounts/{Guid.NewGuid()}/role",
                new UpdateAccountRoleRequest
                {
                    Role = AppRoles.Operator
                },
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_CannotDemoteFinalActiveAdministrator()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var administrator =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Administrator,
                    cancellationToken);

        using var client =
            administrator.Client;

        string administratorId;
        List<string> temporarilyDeactivatedIds;

        using (var scope =
               _factory.Services.CreateScope())
        {
            var userManager =
                scope.ServiceProvider
                    .GetRequiredService<
                        UserManager<ApplicationUser>>();

            var currentAdministrator =
                await userManager.FindByEmailAsync(
                    administrator.Email);

            Assert.NotNull(currentAdministrator);

            administratorId =
                currentAdministrator.Id;

            var administrators =
                await userManager.GetUsersInRoleAsync(
                    AppRoles.Administrator);

            var otherActiveAdministrators =
                administrators
                    .Where(
                        user =>
                            user.Id != administratorId
                            && user.IsActive)
                    .ToList();

            temporarilyDeactivatedIds =
                otherActiveAdministrators
                    .Select(user => user.Id)
                    .ToList();

            foreach (var otherAdministrator
                     in otherActiveAdministrators)
            {
                otherAdministrator.IsActive = false;

                var updateResult =
                    await userManager.UpdateAsync(
                        otherAdministrator);

                AssertIdentitySucceeded(
                    updateResult,
                    "deactivating another Administrator");
            }
        }

        try
        {
            var response =
                await client.PatchAsJsonAsync(
                    $"/api/accounts/{administratorId}/role",
                    new UpdateAccountRoleRequest
                    {
                        Role = AppRoles.Viewer
                    },
                    cancellationToken);

            Assert.Equal(
                HttpStatusCode.Conflict,
                response.StatusCode);

            using var verificationScope =
                _factory.Services.CreateScope();

            var verificationUserManager =
                verificationScope.ServiceProvider
                    .GetRequiredService<
                        UserManager<ApplicationUser>>();

            var unchangedAdministrator =
                await verificationUserManager
                    .FindByIdAsync(administratorId);

            Assert.NotNull(unchangedAdministrator);
            Assert.True(
                unchangedAdministrator.IsActive);

            var roles =
                await verificationUserManager
                    .GetRolesAsync(
                        unchangedAdministrator);

            var role =
                Assert.Single(roles);

            Assert.Equal(
                AppRoles.Administrator,
                role);

            var stillAuthorizedResponse =
                await client.GetAsync(
                    "/api/audit-records",
                    cancellationToken);

            Assert.Equal(
                HttpStatusCode.OK,
                stillAuthorizedResponse.StatusCode);
        }
        finally
        {
            using var restoreScope =
                _factory.Services.CreateScope();

            var restoreUserManager =
                restoreScope.ServiceProvider
                    .GetRequiredService<
                        UserManager<ApplicationUser>>();

            foreach (var userId
                     in temporarilyDeactivatedIds)
            {
                var user =
                    await restoreUserManager.FindByIdAsync(
                        userId);

                if (user is null)
                {
                    continue;
                }

                user.IsActive = true;

                var restoreResult =
                    await restoreUserManager.UpdateAsync(
                        user);

                AssertIdentitySucceeded(
                    restoreResult,
                    "restoring another Administrator");
            }
        }
    }

    [Fact]
    public async Task UpdateStatus_AsAdministrator_DeactivatesAccountRevokesJwtAndAudits()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var administrator =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Administrator,
                    cancellationToken);

        using var administratorClient =
            administrator.Client;

        var targetEmail =
            $"deactivate-{Guid.NewGuid():N}@test.local";

        var account =
            await CreateAccountAsync(
                administratorClient,
                targetEmail,
                AppRoles.Viewer,
                cancellationToken);

        using var targetClient =
            _factory.CreateClient();

        var loginResponse =
            await targetClient.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest
                {
                    Email = targetEmail,
                    Password = ValidPassword
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

        targetClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        var statusResponse =
            await administratorClient.PatchAsJsonAsync(
                $"/api/accounts/{account.Id}/status",
                new UpdateAccountStatusRequest
                {
                    IsActive = false
                },
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            statusResponse.StatusCode);

        var updatedAccount =
            await statusResponse.Content
                .ReadFromJsonAsync<AccountResponse>(
                    cancellationToken:
                        cancellationToken);

        Assert.NotNull(updatedAccount);
        Assert.False(updatedAccount.IsActive);

        var returnedRole =
            Assert.Single(updatedAccount.Roles);

        Assert.Equal(
            AppRoles.Viewer,
            returnedRole);

        using (var scope =
               _factory.Services.CreateScope())
        {
            var userManager =
                scope.ServiceProvider
                    .GetRequiredService<
                        UserManager<ApplicationUser>>();

            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

            var persistedUser =
                await userManager.FindByIdAsync(
                    account.Id);

            Assert.NotNull(persistedUser);
            Assert.False(persistedUser.IsActive);

            var auditRecord =
                await dbContext.AuditRecords
                    .AsNoTracking()
                    .SingleAsync(
                        record =>
                            record.Action ==
                                AuditActions
                                    .UserAccountStatusChanged
                            && record.EntityId ==
                                account.Id,
                        cancellationToken);

            Assert.Equal(
                administrator.Email,
                auditRecord.UserName);

            Assert.Equal(
                AppRoles.Administrator,
                auditRecord.Role);

            Assert.Equal(
                nameof(ApplicationUser),
                auditRecord.EntityType);

            Assert.NotNull(auditRecord.Details);

            Assert.Contains(
                "\"PreviousIsActive\":true",
                auditRecord.Details);

            Assert.Contains(
                "\"CurrentIsActive\":false",
                auditRecord.Details);
        }

        var oldTokenResponse =
            await targetClient.GetAsync(
                "/api/inventoryitems",
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            oldTokenResponse.StatusCode);

        using var newLoginClient =
            _factory.CreateClient();

        var blockedLoginResponse =
            await newLoginClient.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest
                {
                    Email = targetEmail,
                    Password = ValidPassword
                },
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            blockedLoginResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_AsAdministrator_ReactivatesAccountAndAllowsLogin()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var administrator =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Administrator,
                    cancellationToken);

        using var administratorClient =
            administrator.Client;

        var targetEmail =
            $"reactivate-{Guid.NewGuid():N}@test.local";

        var account =
            await CreateAccountAsync(
                administratorClient,
                targetEmail,
                AppRoles.Operator,
                cancellationToken);

        var deactivateResponse =
            await administratorClient.PatchAsJsonAsync(
                $"/api/accounts/{account.Id}/status",
                new UpdateAccountStatusRequest
                {
                    IsActive = false
                },
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            deactivateResponse.StatusCode);

        var reactivateResponse =
            await administratorClient.PatchAsJsonAsync(
                $"/api/accounts/{account.Id}/status",
                new UpdateAccountStatusRequest
                {
                    IsActive = true
                },
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            reactivateResponse.StatusCode);

        var reactivatedAccount =
            await reactivateResponse.Content
                .ReadFromJsonAsync<AccountResponse>(
                    cancellationToken:
                        cancellationToken);

        Assert.NotNull(reactivatedAccount);
        Assert.True(reactivatedAccount.IsActive);

        var returnedRole =
            Assert.Single(reactivatedAccount.Roles);

        Assert.Equal(
            AppRoles.Operator,
            returnedRole);

        using var loginClient =
            _factory.CreateClient();

        var loginResponse =
            await loginClient.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest
                {
                    Email = targetEmail,
                    Password = ValidPassword
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
        Assert.Contains(
            AppRoles.Operator,
            login.Roles);
    }

    [Fact]
    public async Task UpdateStatus_AsOperator_ReturnsForbidden()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var authenticated =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Operator,
                    cancellationToken);

        using var client =
            authenticated.Client;

        var response =
            await client.PatchAsJsonAsync(
                $"/api/accounts/{Guid.NewGuid()}/status",
                new UpdateAccountStatusRequest
                {
                    IsActive = false
                },
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_ForMissingAccount_ReturnsNotFound()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var administrator =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Administrator,
                    cancellationToken);

        using var client =
            administrator.Client;

        var response =
            await client.PatchAsJsonAsync(
                $"/api/accounts/{Guid.NewGuid()}/status",
                new UpdateAccountStatusRequest
                {
                    IsActive = false
                },
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_CannotDeactivateFinalActiveAdministrator()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var administrator =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Administrator,
                    cancellationToken);

        using var client =
            administrator.Client;

        string administratorId;
        List<string> otherAdministratorIds;

        using (var scope =
               _factory.Services.CreateScope())
        {
            var userManager =
                scope.ServiceProvider
                    .GetRequiredService<
                        UserManager<ApplicationUser>>();

            var currentAdministrator =
                await userManager.FindByEmailAsync(
                    administrator.Email);

            Assert.NotNull(currentAdministrator);

            administratorId =
                currentAdministrator.Id;

            var administrators =
                await userManager.GetUsersInRoleAsync(
                    AppRoles.Administrator);

            otherAdministratorIds =
                administrators
                    .Where(
                        user =>
                            user.Id != administratorId
                            && user.IsActive)
                    .Select(user => user.Id)
                    .ToList();
        }

        try
        {
            using (var scope =
                   _factory.Services.CreateScope())
            {
                var userManager =
                    scope.ServiceProvider
                        .GetRequiredService<
                            UserManager<ApplicationUser>>();

                foreach (var userId
                         in otherAdministratorIds)
                {
                    var otherAdministrator =
                        await userManager.FindByIdAsync(
                            userId);

                    Assert.NotNull(otherAdministrator);

                    otherAdministrator.IsActive = false;

                    var updateResult =
                        await userManager.UpdateAsync(
                            otherAdministrator);

                    AssertIdentitySucceeded(
                        updateResult,
                        "temporarily deactivating another Administrator");
                }
            }

            var response =
                await client.PatchAsJsonAsync(
                    $"/api/accounts/{administratorId}/status",
                    new UpdateAccountStatusRequest
                    {
                        IsActive = false
                    },
                    cancellationToken);

            Assert.Equal(
                HttpStatusCode.Conflict,
                response.StatusCode);

            using var verificationScope =
                _factory.Services.CreateScope();

            var verificationUserManager =
                verificationScope.ServiceProvider
                    .GetRequiredService<
                        UserManager<ApplicationUser>>();

            var unchangedAdministrator =
                await verificationUserManager
                    .FindByIdAsync(administratorId);

            Assert.NotNull(unchangedAdministrator);
            Assert.True(
                unchangedAdministrator.IsActive);

            var roles =
                await verificationUserManager.GetRolesAsync(
                    unchangedAdministrator);

            Assert.Contains(
                AppRoles.Administrator,
                roles);

            var stillAuthorizedResponse =
                await client.GetAsync(
                    "/api/audit-records",
                    cancellationToken);

            Assert.Equal(
                HttpStatusCode.OK,
                stillAuthorizedResponse.StatusCode);
        }
        finally
        {
            using var restoreScope =
                _factory.Services.CreateScope();

            var userManager =
                restoreScope.ServiceProvider
                    .GetRequiredService<
                        UserManager<ApplicationUser>>();

            foreach (var userId
                     in otherAdministratorIds)
            {
                var user =
                    await userManager.FindByIdAsync(
                        userId);

                if (user is null)
                {
                    continue;
                }

                user.IsActive = true;

                var restoreResult =
                    await userManager.UpdateAsync(user);

                AssertIdentitySucceeded(
                    restoreResult,
                    "restoring another Administrator");
            }
        }
    }

    [Fact]
    public async Task GetAccounts_AsOperator_ReturnsForbidden()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var authenticated =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Operator,
                    cancellationToken);

        using var client =
            authenticated.Client;

        var response =
            await client.GetAsync(
                "/api/accounts",
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task GetAccounts_AsAdministrator_ReturnsAccountRolesAndStatuses()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var administrator =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Administrator,
                    cancellationToken);

        using var client =
            administrator.Client;

        var viewerEmail =
            $"listed-viewer-{Guid.NewGuid():N}@test.local";

        var operatorEmail =
            $"listed-operator-{Guid.NewGuid():N}@test.local";

        var viewerAccount =
            await CreateAccountAsync(
                client,
                viewerEmail,
                AppRoles.Viewer,
                cancellationToken);

        var operatorAccount =
            await CreateAccountAsync(
                client,
                operatorEmail,
                AppRoles.Operator,
                cancellationToken);

        var deactivateResponse =
            await client.PatchAsJsonAsync(
                $"/api/accounts/{operatorAccount.Id}/status",
                new UpdateAccountStatusRequest
                {
                    IsActive = false
                },
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            deactivateResponse.StatusCode);

        var response =
            await client.GetAsync(
                "/api/accounts",
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var accounts =
            await response.Content
                .ReadFromJsonAsync<
                    List<AccountResponse>>(
                        cancellationToken:
                            cancellationToken);

        Assert.NotNull(accounts);

        var listedAdministrator =
            Assert.Single(
                accounts,
                account =>
                    account.Email ==
                    administrator.Email);

        Assert.True(
            listedAdministrator.IsActive);

        Assert.Contains(
            AppRoles.Administrator,
            listedAdministrator.Roles);

        var listedViewer =
            Assert.Single(
                accounts,
                account =>
                    account.Id ==
                    viewerAccount.Id);

        Assert.Equal(
            viewerEmail,
            listedViewer.Email);

        Assert.True(
            listedViewer.IsActive);

        Assert.Equal(
            AppRoles.Viewer,
            Assert.Single(
                listedViewer.Roles));

        var listedOperator =
            Assert.Single(
                accounts,
                account =>
                    account.Id ==
                    operatorAccount.Id);

        Assert.Equal(
            operatorEmail,
            listedOperator.Email);

        Assert.False(
            listedOperator.IsActive);

        Assert.Equal(
            AppRoles.Operator,
            Assert.Single(
                listedOperator.Roles));
    }

    private static void AssertIdentitySucceeded(
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

    private static async Task<AccountResponse> CreateAccountAsync(
        HttpClient administratorClient,
        string email,
        string role,
        CancellationToken cancellationToken)
    {
        var response =
            await administratorClient.PostAsJsonAsync(
                "/api/accounts",
                new CreateAccountRequest
                {
                    Email = email,
                    Password = ValidPassword,
                    Role = role
                },
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var account =
            await response.Content
                .ReadFromJsonAsync<AccountResponse>(
                    cancellationToken:
                        cancellationToken);

        Assert.NotNull(account);

        return account;
    }
}