using InventoryReorderPlatform.SupplierMockApi.Behavior;
using InventoryReorderPlatform.SupplierMockApi.Contracts;
using InventoryReorderPlatform.SupplierMockApi.Data;
using InventoryReorderPlatform.SupplierMockApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryReorderPlatform.SupplierMockApi.Controllers;

[ApiController]
[Route("api/supplier-orders")]
public sealed class SupplierOrdersController : ControllerBase
{
    private const int MaximumIdempotencyKeyLength = 200;

    private readonly SupplierDbContext _dbContext;
    private readonly ISupplierBehaviorSimulator _behaviorSimulator;
    private readonly ILogger<SupplierOrdersController> _logger;

    public SupplierOrdersController(
        SupplierDbContext dbContext,
        ISupplierBehaviorSimulator behaviorSimulator,
        ILogger<SupplierOrdersController> logger)
    {
        _dbContext = dbContext;
        _behaviorSimulator = behaviorSimulator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<SupplierOrderResponse>> Create(
        [FromBody] CreateSupplierOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue(
                SupplierApiHeaders.IdempotencyKey,
                out var idempotencyKeyValues)
            || idempotencyKeyValues.Count != 1
            || string.IsNullOrWhiteSpace(idempotencyKeyValues[0]))
        {
            ModelState.AddModelError(
                SupplierApiHeaders.IdempotencyKey,
                $"A single non-empty '{SupplierApiHeaders.IdempotencyKey}' header is required.");

            return ValidationProblem(ModelState);
        }

        var idempotencyKey =
            idempotencyKeyValues[0]!.Trim();

        if (idempotencyKey.Length > MaximumIdempotencyKeyLength)
        {
            ModelState.AddModelError(
                SupplierApiHeaders.IdempotencyKey,
                $"The idempotency key cannot exceed {MaximumIdempotencyKeyLength} characters.");

            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            ModelState.AddModelError(
                nameof(request.Sku),
                "SKU cannot be empty or whitespace.");

            return ValidationProblem(ModelState);
        }

        if (request.TriggeredAtUtc == default)
        {
            ModelState.AddModelError(
                nameof(request.TriggeredAtUtc),
                "TriggeredAtUtc is required.");

            return ValidationProblem(ModelState);
        }

        if (request.TriggeredAtUtc.Kind != DateTimeKind.Utc)
        {
            ModelState.AddModelError(
                nameof(request.TriggeredAtUtc),
                "TriggeredAtUtc must be expressed in UTC.");

            return ValidationProblem(ModelState);
        }

        var normalizedSku = request.Sku.Trim();

        var existingOrder = await _dbContext.SupplierOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(
                order => order.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (existingOrder is not null)
        {
            return BuildExistingOrderResult(
                existingOrder,
                request,
                normalizedSku);
        }

        var behaviorResult =
    await _behaviorSimulator.EvaluateAsync(
        idempotencyKey,
        cancellationToken);

        if (behaviorResult.Outcome ==
            SupplierBehaviorOutcome.TransientFailure)
        {
            Response.Headers.Append(
                "Retry-After",
                "1");

            _logger.LogWarning(
                "Simulated transient supplier failure for " +
                "idempotency key {IdempotencyKey} on attempt " +
                "{AttemptNumber}.",
                idempotencyKey,
                behaviorResult.AttemptNumber);

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails
                {
                    Status =
                        StatusCodes.Status503ServiceUnavailable,
                    Title = "Transient supplier failure",
                    Detail = behaviorResult.Message
                });
        }

        if (behaviorResult.Outcome ==
            SupplierBehaviorOutcome.PermanentRejection)
        {
            _logger.LogWarning(
                "Simulated permanent supplier rejection for " +
                "idempotency key {IdempotencyKey}.",
                idempotencyKey);

            return UnprocessableEntity(
                new ProblemDetails
                {
                    Status =
                        StatusCodes.Status422UnprocessableEntity,
                    Title = "Supplier order rejected",
                    Detail = behaviorResult.Message
                });
        }

        var supplierOrder = new SupplierOrder
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = idempotencyKey,
            ReorderEventId = request.ReorderEventId,
            InventoryItemId = request.InventoryItemId,
            Sku = normalizedSku,
            RequestedQuantity = request.RequestedQuantity,
            TriggeredAtUtc = request.TriggeredAtUtc,
            Status = SupplierOrderStatuses.Accepted,
            AcceptedAtUtc = DateTime.UtcNow
        };

        _dbContext.SupplierOrders.Add(supplierOrder);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // Another request may have inserted this idempotency key
            // after our initial lookup but before our insert completed.
            _logger.LogWarning(
                exception,
                "A concurrent supplier-order insert occurred for idempotency key {IdempotencyKey}.",
                idempotencyKey);

            _dbContext.ChangeTracker.Clear();

            existingOrder = await _dbContext.SupplierOrders
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    order => order.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (existingOrder is null)
            {
                throw;
            }

            return BuildExistingOrderResult(
                existingOrder,
                request,
                normalizedSku);
        }

        _logger.LogInformation(
            "Accepted supplier order {SupplierOrderId} for reorder event {ReorderEventId}.",
            supplierOrder.Id,
            supplierOrder.ReorderEventId);

        return StatusCode(
            StatusCodes.Status201Created,
            MapToResponse(supplierOrder));
    }

    private ActionResult<SupplierOrderResponse>
        BuildExistingOrderResult(
            SupplierOrder existingOrder,
            CreateSupplierOrderRequest request,
            string normalizedSku)
    {
        if (!PayloadMatches(
                existingOrder,
                request,
                normalizedSku))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Idempotency key conflict",
                Detail =
                    "The supplied idempotency key has already been used " +
                    "for a different supplier-order payload."
            });
        }

        _logger.LogInformation(
            "Returning existing supplier order {SupplierOrderId} for duplicate idempotency key.",
            existingOrder.Id);

        return Ok(MapToResponse(existingOrder));
    }

    private static bool PayloadMatches(
        SupplierOrder existingOrder,
        CreateSupplierOrderRequest request,
        string normalizedSku)
    {
        return
            existingOrder.ReorderEventId == request.ReorderEventId
            && existingOrder.InventoryItemId
                == request.InventoryItemId
            && string.Equals(
                existingOrder.Sku,
                normalizedSku,
                StringComparison.Ordinal)
            && existingOrder.RequestedQuantity
                == request.RequestedQuantity
            && existingOrder.TriggeredAtUtc
                == request.TriggeredAtUtc;
    }

    private static SupplierOrderResponse MapToResponse(
        SupplierOrder supplierOrder)
    {
        return new SupplierOrderResponse
        {
            SupplierOrderId = supplierOrder.Id,
            IdempotencyKey = supplierOrder.IdempotencyKey,
            ReorderEventId = supplierOrder.ReorderEventId,
            InventoryItemId = supplierOrder.InventoryItemId,
            Sku = supplierOrder.Sku,
            RequestedQuantity =
                supplierOrder.RequestedQuantity,
            TriggeredAtUtc = supplierOrder.TriggeredAtUtc,
            Status = supplierOrder.Status,
            AcceptedAtUtc = supplierOrder.AcceptedAtUtc
        };
    }
}