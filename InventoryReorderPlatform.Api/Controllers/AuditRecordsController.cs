using InventoryReorderPlatform.Api.DTOs;
using InventoryReorderPlatform.Api.Security;
using InventoryReorderPlatform.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryReorderPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/audit-records")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public class AuditRecordsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public AuditRecordsController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        [EndpointSummary("List audit records")]
        [EndpointDescription(
            "Returns application audit records ordered from newest to oldest. " +
            "Each record identifies the acting user and role, the action performed, " +
            "the affected entity, optional details, and the occurrence time.")]
        [ProducesResponseType<IEnumerable<AuditRecordResponse>>(
            StatusCodes.Status200OK,
            "application/json")]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IEnumerable<AuditRecordResponse>>> GetAll(
            CancellationToken cancellationToken)
        {
            var auditRecords = await _dbContext.AuditRecords
                .AsNoTracking()
                .OrderByDescending(record => record.OccurredAt)
                .Select(record => new AuditRecordResponse
                {
                    Id = record.Id,
                    UserName = record.UserName,
                    Role = record.Role,
                    Action = record.Action,
                    EntityType = record.EntityType,
                    EntityId = record.EntityId,
                    Details = record.Details,
                    OccurredAt = record.OccurredAt
                })
                .ToListAsync(cancellationToken);

            return Ok(auditRecords);
        }
    }
}