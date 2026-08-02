# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["InventoryReorderPlatform.SupplierMockApi/InventoryReorderPlatform.SupplierMockApi.csproj", "InventoryReorderPlatform.SupplierMockApi/"]
COPY ["InventoryReorderPlatform.ServiceDefaults/InventoryReorderPlatform.ServiceDefaults.csproj", "InventoryReorderPlatform.ServiceDefaults/"]

RUN dotnet restore "./InventoryReorderPlatform.SupplierMockApi/InventoryReorderPlatform.SupplierMockApi.csproj"

COPY . .

WORKDIR "/src/InventoryReorderPlatform.SupplierMockApi"

RUN dotnet build \
    "./InventoryReorderPlatform.SupplierMockApi.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish \
    "./InventoryReorderPlatform.SupplierMockApi.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "InventoryReorderPlatform.SupplierMockApi.dll"]