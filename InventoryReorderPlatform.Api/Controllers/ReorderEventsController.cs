using InventoryReorderPlatform.Api.Data;
using InventoryReorderPlatform.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryReorderPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReorderEventsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public ReorderEventsController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReorderEventResponse>>> GetAll()
        {
            var reorderEvents = await _dbContext.ReorderEvents
                .OrderByDescending(r => r.TriggeredAt)
                .Select(r => new ReorderEventResponse
                {
                    Id = r.Id,
                    InventoryItemId = r.InventoryItemId,
                    QuantityAtTrigger = r.QuantityAtTrigger,
                    TriggeredAt = r.TriggeredAt,
                    Status = r.Status
                })
                .ToListAsync();

            return Ok(reorderEvents);
        }
    }
}
