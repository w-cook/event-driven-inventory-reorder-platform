using InventoryReorderPlatform.Api.DTOs;
using InventoryReorderPlatform.Api.Security;
using InventoryReorderPlatform.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryReorderPlatform.Api.Controllers;

[ApiController]
[Route("api/operations")]
[Authorize(Policy = AppPolicies.InventoryRead)]
public class OperationsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<OperationsController> _logger;

    public OperationsController(
        AppDbContext dbContext,
        ILogger<OperationsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("health")]
    [EndpointSummary("Get operations health")]
    [EndpointDescription(
        "Checks application database connectivity and returns inventory-item " +
        "and reorder-event counts when the database is available.")]
    [ProducesResponseType<OperationsHealthResponse>(
        StatusCodes.Status200OK,
        "application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<OperationsHealthResponse>(
        StatusCodes.Status503ServiceUnavailable,
        "application/json")]
    public async Task<ActionResult<OperationsHealthResponse>> GetHealth(
        CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(
                cancellationToken);

            if (!canConnect)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new OperationsHealthResponse(
                        Status: "Unhealthy",
                        DatabaseStatus: "Unavailable",
                        InventoryItemCount: null,
                        ReorderEventCount: null,
                        CheckedAt: DateTime.UtcNow));
            }

            var inventoryItemCount = await _dbContext.InventoryItems
                .CountAsync(cancellationToken);

            var reorderEventCount = await _dbContext.ReorderEvents
                .CountAsync(cancellationToken);

            return Ok(
                new OperationsHealthResponse(
                    Status: "Healthy",
                    DatabaseStatus: "Connected",
                    InventoryItemCount: inventoryItemCount,
                    ReorderEventCount: reorderEventCount,
                    CheckedAt: DateTime.UtcNow));
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to query the database for operations health.");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new OperationsHealthResponse(
                    Status: "Unhealthy",
                    DatabaseStatus: "Unavailable",
                    InventoryItemCount: null,
                    ReorderEventCount: null,
                    CheckedAt: DateTime.UtcNow));
        }
    }
}