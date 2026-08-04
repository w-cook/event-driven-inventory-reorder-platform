using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace InventoryReorderPlatform.Api.OpenApi;

internal sealed class BearerSecuritySchemeTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var authenticationSchemes =
            await authenticationSchemeProvider
                .GetAllSchemesAsync();

        var bearerIsRegistered =
            authenticationSchemes.Any(
                scheme =>
                    scheme.Name ==
                    JwtBearerDefaults.AuthenticationScheme);

        if (!bearerIsRegistered)
        {
            return;
        }

        document.Components ??=
            new OpenApiComponents();

        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[
            JwtBearerDefaults.AuthenticationScheme] =
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description =
                    "Enter the JWT access token returned by " +
                    "POST /api/auth/login."
            };
    }
}