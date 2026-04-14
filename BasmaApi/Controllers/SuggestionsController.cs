using BasmaApi.Contracts;
using BasmaApi.Data;
using BasmaApi.Models;
using BasmaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BasmaApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class SuggestionsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public SuggestionsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SuggestionWithVoteResponse>>> List(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var currentMemberId = GetCurrentMemberId();
        if (currentMemberId == Guid.Empty)
            return Unauthorized();

        var query = _dbContext.Suggestions
            .Include(s => s.CreatedByMember)
            .Include(s => s.Votes.Where(v => v.VotedByMemberId == currentMemberId))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<SuggestionStatus>(status, true, out var statusEnum))
            {
                query = query.Where(s => s.Status == statusEnum);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(s =>
                s.Title.ToLower().Contains(searchLower) ||
                s.Description.ToLower().Contains(searchLower));
        }

        var suggestions = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(suggestions.Select(s => MapToVoteResponse(s, currentMemberId)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SuggestionWithVoteResponse>> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var currentMemberId = GetCurrentMemberId();
        if (currentMemberId == Guid.Empty)
            return Unauthorized();

        var suggestion = await _dbContext.Suggestions
            .Include(s => s.CreatedByMember)
            .Include(s => s.Votes.Where(v => v.VotedByMemberId == currentMemberId))
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (suggestion is null)
            return NotFound();

        return Ok(MapToVoteResponse(suggestion, currentMemberId));
    }

    [HttpPost]
    public async Task<ActionResult<SuggestionResponse>> Create(
        SuggestionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentMemberId = GetCurrentMemberId();
        if (currentMemberId == Guid.Empty)
            return Unauthorized();

        var currentMember = await _dbContext.Members.FindAsync(new object[] { currentMemberId }, cancellationToken);
        if (currentMember is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            return BadRequest("العنوان والوصف مطلوبان");

        var suggestion = new Suggestion
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByMemberId = currentMemberId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Status = SuggestionStatus.Open,
            AcceptanceCount = 0,
            RejectionCount = 0
        };

        _dbContext.Suggestions.Add(suggestion);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = suggestion.Id }, MapToResponse(suggestion, currentMember));
    }

    [HttpPost("{id}/vote")]
    public async Task<ActionResult<SuggestionWithVoteResponse>> Vote(
        Guid id,
        SuggestionVoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentMemberId = GetCurrentMemberId();
        if (currentMemberId == Guid.Empty)
            return Unauthorized();

        var suggestion = await _dbContext.Suggestions
            .Include(s => s.CreatedByMember)
            .Include(s => s.Votes)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (suggestion is null)
            return NotFound();

        // Check if user already voted
        var existingVote = suggestion.Votes.FirstOrDefault(v => v.VotedByMemberId == currentMemberId);
        if (existingVote is not null)
        {
            // Update existing vote
            // First adjust counts if vote changed
            if (existingVote.IsAcceptance != request.IsAcceptance)
            {
                if (existingVote.IsAcceptance)
                    suggestion.AcceptanceCount = Math.Max(0, suggestion.AcceptanceCount - 1);
                else
                    suggestion.RejectionCount = Math.Max(0, suggestion.RejectionCount - 1);

                if (request.IsAcceptance)
                    suggestion.AcceptanceCount++;
                else
                    suggestion.RejectionCount++;

                existingVote.IsAcceptance = request.IsAcceptance;
            }
        }
        else
        {
            // Create new vote
            var vote = new SuggestionVote
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow,
                SuggestionId = id,
                VotedByMemberId = currentMemberId,
                IsAcceptance = request.IsAcceptance
            };

            suggestion.Votes.Add(vote);

            if (request.IsAcceptance)
                suggestion.AcceptanceCount++;
            else
                suggestion.RejectionCount++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Reload to get updated vote status
        var updatedSuggestion = await _dbContext.Suggestions
            .Include(s => s.CreatedByMember)
            .Include(s => s.Votes.Where(v => v.VotedByMemberId == currentMemberId))
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        return Ok(MapToVoteResponse(updatedSuggestion!, currentMemberId));
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "President,VicePresident,CentralMember")]
    public async Task<ActionResult<SuggestionResponse>> ChangeStatus(
        Guid id,
        SuggestionStatusChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentMemberId = GetCurrentMemberId();
        var currentMember = await _dbContext.Members.FindAsync(new object[] { currentMemberId }, cancellationToken);

        if (currentMember is null || !AccessControl.CanManageUsers(currentMember))
            return Forbid();

        if (!Enum.TryParse<SuggestionStatus>(request.NewStatus, true, out var newStatus))
            return BadRequest("حالة غير صحيحة");

        var suggestion = await _dbContext.Suggestions
            .Include(s => s.CreatedByMember)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (suggestion is null)
            return NotFound();

        suggestion.Status = newStatus;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapToResponse(suggestion, suggestion.CreatedByMember!));
    }

    private Guid GetCurrentMemberId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idClaim?.Value, out var id) ? id : Guid.Empty;
    }

    private SuggestionWithVoteResponse MapToVoteResponse(Suggestion suggestion, Guid currentMemberId)
    {
        var currentVote = suggestion.Votes?.FirstOrDefault(v => v.VotedByMemberId == currentMemberId);

        return new SuggestionWithVoteResponse(
            suggestion.Id,
            suggestion.Title,
            suggestion.Description,
            suggestion.Status.ToString(),
            suggestion.AcceptanceCount,
            suggestion.RejectionCount,
            currentVote?.IsAcceptance,
            suggestion.CreatedByMember?.FullName ?? "غير معروف",
            suggestion.CreatedByMember?.Role.ToString() ?? "غير معروف",
            suggestion.CreatedAtUtc
        );
    }

    private SuggestionResponse MapToResponse(Suggestion suggestion, Member createdByMember)
    {
        return new SuggestionResponse(
            suggestion.Id,
            suggestion.Title,
            suggestion.Description,
            suggestion.Status.ToString(),
            suggestion.AcceptanceCount,
            suggestion.RejectionCount,
            createdByMember.FullName,
            createdByMember.Role.ToString(),
            suggestion.CreatedAtUtc
        );
    }
}
