using BasmaApi.Contracts;
using BasmaApi.Data;
using BasmaApi.Models;
using BasmaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasmaApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class AuditLogsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public AuditLogsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<PagedAuditLogResponse>> Get([FromQuery] Guid? userId, [FromQuery] string? actionType, [FromQuery] string? entityName, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (!AccessControl.CanViewAuditLogs(currentMember))
        {
            return Forbid();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.AuditLogs.AsNoTracking().AsQueryable();

        if (userId is not null)
        {
            query = query.Where(item => item.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(actionType))
        {
            query = query.Where(item => item.ActionType == actionType);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(item => item.EntityName == entityName);
        }

        if (fromUtc is not null)
        {
            query = query.Where(item => item.TimestampUtc >= fromUtc.Value);
        }

        if (toUtc is not null)
        {
            query = query.Where(item => item.TimestampUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(item =>
                item.UserName.Contains(normalized) ||
                item.ActionType.Contains(normalized) ||
                item.EntityName.Contains(normalized) ||
                (item.EntityId != null && item.EntityId.Contains(normalized)) ||
                (item.OldValuesJson != null && item.OldValuesJson.Contains(normalized)) ||
                (item.NewValuesJson != null && item.NewValuesJson.Contains(normalized)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.TimestampUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AuditLogResponse(
                item.Id,
                item.UserId,
                item.UserName,
                item.ActionType,
                item.EntityName,
                item.EntityId,
                item.OldValuesJson,
                item.NewValuesJson,
                item.TimestampUtc,
                item.IPAddress))
            .ToListAsync(cancellationToken);

        return Ok(new PagedAuditLogResponse(items, totalCount, page, pageSize));
    }

    private async Task<Member?> GetCurrentMemberAsync(CancellationToken cancellationToken)
    {
        var memberId = User.GetMemberId();
        if (memberId is null)
        {
            return null;
        }

        return await _dbContext.Members
            .Include(member => member.PermissionGrants)
            .FirstOrDefaultAsync(member => member.Id == memberId.Value, cancellationToken);
    }
}