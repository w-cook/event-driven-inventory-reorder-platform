using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace InventoryReorderPlatform.Api.Security;

public sealed class DemoAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public DemoAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(
                DemoAuthenticationDefaults.HeaderName,
                out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var suppliedUser = headerValues
            .ToString()
            .Trim()
            .ToLowerInvariant();

        var demoUser = suppliedUser switch
        {
            "viewer" => new DemoUser(
                UserName: "viewer@example.local",
                Role: AppRoles.Viewer),

            "operator" => new DemoUser(
                UserName: "operator@example.local",
                Role: AppRoles.Operator),

            "admin" => new DemoUser(
                UserName: "admin@example.local",
                Role: AppRoles.Administrator),

            _ => null
        };

        if (demoUser is null)
        {
            return Task.FromResult(
                AuthenticateResult.Fail(
                    $"Unknown demo user '{suppliedUser}'."));
        }

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                demoUser.UserName),

            new Claim(
                ClaimTypes.Name,
                demoUser.UserName),

            new Claim(
                ClaimTypes.Role,
                demoUser.Role)
        };

        var identity = new ClaimsIdentity(
            claims,
            DemoAuthenticationDefaults.Scheme);

        var principal = new ClaimsPrincipal(identity);

        var ticket = new AuthenticationTicket(
            principal,
            DemoAuthenticationDefaults.Scheme);

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }

    private sealed record DemoUser(
        string UserName,
        string Role);
}