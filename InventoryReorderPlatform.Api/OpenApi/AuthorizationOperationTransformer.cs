using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace InventoryReorderPlatform.Api.OpenApi;

internal sealed class AuthorizationOperationTransformer
    : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var endpointMetadata =
            context.Description
                .ActionDescriptor
                .EndpointMetadata;

        var allowsAnonymousAccess =
            endpointMetadata
                .OfType<IAllowAnonymous>()
                .Any();

        var requiresAuthorization =
            endpointMetadata
                .OfType<IAuthorizeData>()
                .Any();

        if (allowsAnonymousAccess ||
            !requiresAuthorization)
        {
            return Task.CompletedTask;
        }

        operation.Security ??=
            new List<OpenApiSecurityRequirement>();

        operation.Security.Add(
            new OpenApiSecurityRequirement
            {
                [
                    new OpenApiSecuritySchemeReference(
                        JwtBearerDefaults.AuthenticationScheme,
                        context.Document)
                ] = []
            });

        return Task.CompletedTask;
    }
}