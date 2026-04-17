using BasmaApi.Contracts;
using BasmaApi.Data;
using BasmaApi.Models;
using BasmaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BasmaApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class NewsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<NewsController> _logger;

    public NewsController(AppDbContext dbContext, ILogger<NewsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NewsItemResponse>>> List(CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        var role = currentMember.Role;
        var memberId = currentMember.Id;

        var news = await _dbContext.NewsPosts
            .AsNoTracking()
            .Include(item => item.CreatedByMember)
            .Include(item => item.TargetRoles)
            .Include(item => item.TargetMembers)
            .Where(item => item.AudienceType == NewsAudienceType.All
                || (item.AudienceType == NewsAudienceType.Roles
                    && (!item.TargetRoles.Any() || item.TargetRoles.Any(target => target.Role == role)))
                || (item.AudienceType == NewsAudienceType.Members
                    && (!item.TargetMembers.Any() || item.TargetMembers.Any(target => target.MemberId == memberId))))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "News list for {MemberId} role {Role} returned {Count} items.",
            currentMember.Id,
            currentMember.Role,
            news.Count);

        return Ok(news.Select(MapNews));
    }

    [HttpPost]
    public async Task<ActionResult<NewsItemResponse>> Create([FromBody] NewsCreateRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (currentMember.Role is not (MemberRole.President or MemberRole.VicePresident))
        {
            return Forbid();
        }

        if (!Enum.TryParse<NewsAudienceType>(request.AudienceType, ignoreCase: true, out var audienceType))
        {
            return BadRequest(new { message = "نوع الجمهور غير صالح." });
        }

        var targetRoles = new List<MemberRole>();
        if (audienceType == NewsAudienceType.Roles)
        {
            if (request.TargetRoles is null || request.TargetRoles.Count == 0)
            {
                return BadRequest(new { message = "حدد دورًا واحدًا على الأقل عند استهداف الأدوار." });
            }

            foreach (var roleText in request.TargetRoles)
            {
                if (!Enum.TryParse<MemberRole>(roleText, ignoreCase: true, out var role))
                {
                    return BadRequest(new { message = $"الدور غير صالح: {roleText}" });
                }

                targetRoles.Add(role);
            }

            targetRoles = targetRoles.Distinct().ToList();
        }

        var targetMemberIds = new List<Guid>();
        if (audienceType == NewsAudienceType.Members)
        {
            if (request.TargetMemberIds is null || request.TargetMemberIds.Count == 0)
            {
                return BadRequest(new { message = "حدد عضوًا واحدًا على الأقل عند الاستهداف المباشر." });
            }

            targetMemberIds = request.TargetMemberIds.Distinct().ToList();
            var existingMembersCount = await _dbContext.Members.CountAsync(member => targetMemberIds.Contains(member.Id), cancellationToken);
            if (existingMembersCount != targetMemberIds.Count)
            {
                return BadRequest(new { message = "توجد أعضاء غير موجودين ضمن قائمة الاستهداف." });
            }
        }

        var newsPost = new NewsPost
        {
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            AudienceType = audienceType,
            CreatedByMemberId = currentMember.Id
        };

        foreach (var role in targetRoles)
        {
            newsPost.TargetRoles.Add(new NewsTargetRole { Role = role });
        }

        foreach (var targetMemberId in targetMemberIds)
        {
            newsPost.TargetMembers.Add(new NewsTargetMember { MemberId = targetMemberId });
        }

        _dbContext.NewsPosts.Add(newsPost);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await _dbContext.NewsPosts
            .AsNoTracking()
            .Include(item => item.CreatedByMember)
            .Include(item => item.TargetRoles)
            .Include(item => item.TargetMembers)
            .FirstAsync(item => item.Id == newsPost.Id, cancellationToken);

        return CreatedAtAction(nameof(List), MapNews(created));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (currentMember.Role is not (MemberRole.President or MemberRole.VicePresident))
        {
            return Forbid();
        }

        var newsPost = await _dbContext.NewsPosts
            .Include(item => item.TargetRoles)
            .Include(item => item.TargetMembers)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (newsPost is null)
        {
            return NotFound();
        }

        _dbContext.NewsPosts.Remove(newsPost);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<Member?> GetCurrentMemberAsync(CancellationToken cancellationToken)
    {
        var memberId = User.GetMemberId();
        if (memberId is null)
        {
            return null;
        }

        return await _dbContext.Members.FirstOrDefaultAsync(member => member.Id == memberId.Value, cancellationToken);
    }

    private static NewsItemResponse MapNews(NewsPost post)
    {
        return new NewsItemResponse(
            post.Id,
            post.Title,
            post.Content,
            post.AudienceType.ToString(),
            post.CreatedByMemberId,
            post.CreatedByMember.FullName,
            post.CreatedAtUtc,
            post.TargetRoles.Select(item => item.Role.ToString()).ToList(),
            post.TargetMembers.Select(item => item.MemberId).ToList());
    }
}