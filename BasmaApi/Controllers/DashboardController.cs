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
public sealed class DashboardController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public DashboardController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<DashboardResponse>> GetOverview(CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        var topMembers = await _dbContext.Members
            .AsNoTracking()
            .OrderByDescending(member => member.Points)
            .ThenBy(member => member.FullName)
            .Take(10)
            .ToListAsync(cancellationToken);

        var leaderboard = topMembers
            .Select((member, index) => new LeaderboardEntryResponse(
                member.Id,
                member.FullName,
                member.Role.ToString(),
                member.Points,
                index + 1))
            .ToList();

        var openComplaints = await _dbContext.Complaints.CountAsync(
            complaint => complaint.Status == ComplaintStatus.Open || complaint.Status == ComplaintStatus.InReview,
            cancellationToken);

        return Ok(new DashboardResponse(
            currentMember.Id,
            currentMember.FullName,
            currentMember.Role.ToString(),
            currentMember.Points,
            await _dbContext.Members.CountAsync(cancellationToken),
            openComplaints,
            leaderboard));
    }

    [HttpGet("leaderboard")]
    public async Task<ActionResult<IReadOnlyList<LeaderboardEntryResponse>>> GetLeaderboard(CancellationToken cancellationToken)
    {
        var members = await _dbContext.Members
            .AsNoTracking()
            .OrderByDescending(member => member.Points)
            .ThenBy(member => member.FullName)
            .Take(10)
            .ToListAsync(cancellationToken);

        var leaderboard = members
            .Select((member, index) => new LeaderboardEntryResponse(
                member.Id,
                member.FullName,
                member.Role.ToString(),
                member.Points,
                index + 1))
            .ToList();

        return Ok(leaderboard);
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