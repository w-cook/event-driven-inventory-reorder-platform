using InventoryReorderPlatform.Api.DTOs;
using InventoryReorderPlatform.Api.Services;
using InventoryReorderPlatform.Contracts.Messages;
using InventoryReorderPlatform.Data;
using InventoryReorderPlatform.Data.Models;
using InventoryReorderPlatform.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryReorderPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryItemsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly ReorderMessagePublisher _reorderMessagePublisher;
        private readonly ILogger<InventoryItemsController> _logger;

        public InventoryItemsController(
            AppDbContext dbContext,
            ReorderMessagePublisher reorderMessagePublisher,
            ILogger<InventoryItemsController> logger)
        {
            _dbContext = dbContext;
            _reorderMessagePublisher = reorderMessagePublisher;
            _logger = logger;
        }

        [HttpGet("{id:int}")]
        [Authorize(Policy = AppPolicies.InventoryRead)]
        public async Task<ActionResult<InventoryItemResponse>> GetById([FromRoute] int id)
        {
            var inventoryItem = await _dbContext.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == id);

            if (inventoryItem == null)
            {
                return NotFound($"InventoryItemId '{id}' does not exist.");
            }

            return Ok(MapInventoryItemToResponse(inventoryItem));
        }

        [HttpGet]
        [Authorize(Policy = AppPolicies.InventoryRead)]
        public async Task<ActionResult<IEnumerable<InventoryItemResponse>>> GetAll()
        {
            var inventoryItems = await _dbContext.InventoryItems
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new InventoryItemResponse
                {
                    Id = i.Id,
                    Name = i.Name,
                    Sku = i.Sku,
                    QuantityOnHand = i.QuantityOnHand,
                    ReorderThreshold = i.ReorderThreshold,
                    Status = i.Status,
                    CreatedAt = i.CreatedAt,
                    UpdatedAt = i.UpdatedAt
                })
                .ToListAsync();

            return Ok(inventoryItems);
        }

        [HttpPost]
        [Authorize(Policy = AppPolicies.InventoryOperate)]
        public async Task<ActionResult<InventoryItemResponse>> Create(CreateInventoryItemRequest request)
        {
            var inventoryItem = new InventoryItem
            {
                Name = request.Name.Trim(),
                Sku = request.Sku.Trim(),
                QuantityOnHand = request.QuantityOnHand,
                ReorderThreshold = request.ReorderThreshold,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.InventoryItems.Add(inventoryItem);
            await _dbContext.SaveChangesAsync();

            var newReorderEvent = await ApplyInventoryStatusWorkflowAsync(inventoryItem);
            await _dbContext.SaveChangesAsync();

            if (newReorderEvent != null)
            {
                try
                {
                    await PublishReorderMessageAsync(newReorderEvent, inventoryItem);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish reorder message for event {ReorderEventId}.", newReorderEvent.Id);
                    throw;
                }
            }

            return CreatedAtAction(
                nameof(GetById),
                new { id = inventoryItem.Id },
                MapInventoryItemToResponse(inventoryItem));
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = AppPolicies.InventoryOperate)]
        public async Task<ActionResult<InventoryItemResponse>> UpdateInventoryItem(
            [FromRoute] int id,
            UpdateInventoryItemRequest request)
        {
            var inventoryItem = await _dbContext.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == id);

            if (inventoryItem == null)
            {
                return NotFound($"InventoryItemId '{id}' does not exist.");
            }

            inventoryItem.Name = request.Name.Trim();
            inventoryItem.Sku = request.Sku.Trim();
            inventoryItem.QuantityOnHand = request.QuantityOnHand;
            inventoryItem.ReorderThreshold = request.ReorderThreshold;

            var newReorderEvent = await ApplyInventoryStatusWorkflowAsync(inventoryItem);

            if (newReorderEvent == null)
            {
                inventoryItem.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            if (newReorderEvent != null)
            {
                try
                {
                    await PublishReorderMessageAsync(newReorderEvent, inventoryItem);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish reorder message for event {ReorderEventId}.", newReorderEvent.Id);
                    throw;
                }
            }

            return Ok(MapInventoryItemToResponse(inventoryItem));
        }

        private async Task<ReorderEvent?> ApplyInventoryStatusWorkflowAsync(InventoryItem inventoryItem)
        {
            var targetStatus = inventoryItem.QuantityOnHand <= inventoryItem.ReorderThreshold
                ? "ReorderPending"
                : "Active";

            var oldStatus = inventoryItem.Status;

            if (targetStatus == oldStatus)
            {
                return null;
            }

            var historyEntry = new ReorderHistory
            {
                InventoryItemId = inventoryItem.Id,
                OldStatus = oldStatus,
                NewStatus = targetStatus,
                ChangedAt = DateTime.UtcNow
            };

            await _dbContext.ReorderHistoryEntries.AddAsync(historyEntry);

            ReorderEvent? newReorderEvent = null;

            if (targetStatus == "ReorderPending")
            {
                newReorderEvent = new ReorderEvent
                {
                    InventoryItemId = inventoryItem.Id,
                    QuantityAtTrigger = inventoryItem.QuantityOnHand,
                    TriggeredAt = DateTime.UtcNow,
                    Status = "Pending"
                };

                await _dbContext.ReorderEvents.AddAsync(newReorderEvent);
            }

            inventoryItem.Status = targetStatus;
            inventoryItem.UpdatedAt = DateTime.UtcNow;

            return newReorderEvent;
        }

        private async Task PublishReorderMessageAsync(ReorderEvent reorderEvent, InventoryItem inventoryItem)
        {
            await _reorderMessagePublisher.PublishAsync(new ReorderRequestedMessage
            {
                ReorderEventId = reorderEvent.Id,
                InventoryItemId = inventoryItem.Id,
                Sku = inventoryItem.Sku,
                QuantityAtTrigger = reorderEvent.QuantityAtTrigger,
                TriggeredAt = reorderEvent.TriggeredAt
            });
        }

        private static InventoryItemResponse MapInventoryItemToResponse(InventoryItem inventoryItem)
        {
            return new InventoryItemResponse
            {
                Id = inventoryItem.Id,
                Name = inventoryItem.Name,
                Sku = inventoryItem.Sku,
                QuantityOnHand = inventoryItem.QuantityOnHand,
                ReorderThreshold = inventoryItem.ReorderThreshold,
                Status = inventoryItem.Status,
                CreatedAt = inventoryItem.CreatedAt,
                UpdatedAt = inventoryItem.UpdatedAt
            };
        }
    }
}