using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InventoryReorderPlatform.Api.Security;
using InventoryReorderPlatform.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InventoryReorderPlatform.Api.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtOptions _options;

    public JwtTokenService(
        UserManager<ApplicationUser> userManager,
        IOptions<JwtOptions> options)
    {
        _userManager = userManager;
        _options = options.Value;
    }

    public async Task<AccessTokenResult> CreateAsync(
        ApplicationUser user)
    {
        ValidateOptions();

        var roles =
            await _userManager.GetRolesAsync(user);

        var securityStamp =
            await _userManager.GetSecurityStampAsync(user);

        if (string.IsNullOrWhiteSpace(securityStamp))
        {
            throw new InvalidOperationException(
                "The user does not have a security stamp.");
        }

        var now = DateTime.UtcNow;

        var expiresAtUtc =
            now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                user.Id),

            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString()),

            new(
                ClaimTypes.NameIdentifier,
                user.Id),

            new(
                ClaimTypes.Name,
                user.UserName
                ?? user.Email
                ?? user.Id),

            new(
                JwtClaimNames.SecurityStamp,
                securityStamp)
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(
                new Claim(
                    JwtRegisteredClaimNames.Email,
                    user.Email));
        }

        claims.AddRange(
            roles.Select(
                role =>
                    new Claim(
                        ClaimTypes.Role,
                        role)));

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                _options.SigningKey));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAtUtc,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();

        var token =
            handler.CreateToken(descriptor);

        return new AccessTokenResult(
            handler.WriteToken(token),
            expiresAtUtc);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Issuer))
        {
            throw new InvalidOperationException(
                "Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.Audience))
        {
            throw new InvalidOperationException(
                "Jwt:Audience is required.");
        }

        if (Encoding.UTF8.GetByteCount(
                _options.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be at least 32 bytes.");
        }

        if (_options.AccessTokenMinutes <= 0)
        {
            throw new InvalidOperationException(
                "Jwt:AccessTokenMinutes must be greater than zero.");
        }
    }
}