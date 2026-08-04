using InventoryReorderPlatform.Data;
using InventoryReorderPlatform.Api.DTOs;
using InventoryReorderPlatform.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryReorderPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = AppPolicies.InventoryRead)]
    public class ReorderEventsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public ReorderEventsController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        [EndpointSummary("List reorder events")]
        [EndpointDescription(
            "Returns reorder workflow records ordered from newest to oldest, " +
            "including the stock quantity at the time of the trigger, the requested " +
            "reorder quantity, processing status, and supplier-submission details.")]
        [ProducesResponseType<IEnumerable<ReorderEventResponse>>(
            StatusCodes.Status200OK,
            "application/json")]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IEnumerable<ReorderEventResponse>>> GetAll()
        {
            var reorderEvents = await _dbContext.ReorderEvents
                .OrderByDescending(r => r.TriggeredAt)
                .Select(r => new ReorderEventResponse
                {
                    Id = r.Id,
                    InventoryItemId = r.InventoryItemId,
                    QuantityAtTrigger = r.QuantityAtTrigger,
                    RequestedQuantity = r.RequestedQuantity,
                    TriggeredAt = r.TriggeredAt,
                    Status = r.Status,
                    SupplierOrderId = r.SupplierOrderId,
                    SupplierOrderStatus = r.SupplierOrderStatus,
                    SupplierAcceptedAtUtc = r.SupplierAcceptedAtUtc,
                    SupplierRejectionReason = r.SupplierRejectionReason
                })
                .ToListAsync();

            return Ok(reorderEvents);
        }
    }
}
