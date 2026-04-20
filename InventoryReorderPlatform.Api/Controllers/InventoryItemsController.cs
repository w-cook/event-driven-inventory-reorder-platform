using InventoryReorderPlatform.Api.Data;
using InventoryReorderPlatform.Api.DTOs;
using InventoryReorderPlatform.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryReorderPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryItemsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public InventoryItemsController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("{id:int}")]
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

            await ApplyInventoryStatusWorkflowAsync(inventoryItem);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetById),
                new { id = inventoryItem.Id },
                MapInventoryItemToResponse(inventoryItem));
        }

        [HttpPut("{id:int}")]
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

            if (!await ApplyInventoryStatusWorkflowAsync(inventoryItem))
            {
                inventoryItem.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            return Ok(MapInventoryItemToResponse(inventoryItem));
        }

        private async Task<bool> ApplyInventoryStatusWorkflowAsync(InventoryItem inventoryItem)
        {
            var targetStatus = inventoryItem.QuantityOnHand <= inventoryItem.ReorderThreshold
                ? "ReorderPending"
                : "Active";

            var oldStatus = inventoryItem.Status;

            if (targetStatus == oldStatus)
            {
                return false;
            }

            var historyEntry = new ReorderHistory
            {
                InventoryItemId = inventoryItem.Id,
                OldStatus = oldStatus,
                NewStatus = targetStatus,
                ChangedAt = DateTime.UtcNow
            };

            await _dbContext.ReorderHistoryEntries.AddAsync(historyEntry);

            if (targetStatus == "ReorderPending")
            {
                var reorderEvent = new ReorderEvent
                {
                    InventoryItemId = inventoryItem.Id,
                    QuantityAtTrigger = inventoryItem.QuantityOnHand,
                    TriggeredAt = DateTime.UtcNow,
                    Status = targetStatus
                };

                await _dbContext.ReorderEvents.AddAsync(reorderEvent);
            }

            inventoryItem.Status = targetStatus;
            inventoryItem.UpdatedAt = DateTime.UtcNow;

            return true;
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