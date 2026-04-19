using InventoryReorderPlatform.Api.Data;
using InventoryReorderPlatform.Api.Models;
using InventoryReorderPlatform.Api.DTOs;
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
                Status = request.Status.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.InventoryItems.Add(inventoryItem);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetById),
                new { id = inventoryItem.Id },
                MapInventoryItemToResponse(inventoryItem));
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