using System.ComponentModel.DataAnnotations;

namespace BasmaApi.Contracts;

public sealed class SuggestionCreateRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Description { get; init; } = string.Empty;
}

public sealed record SuggestionResponse(
    Guid Id,
    string Title,
    string Description,
    string Status,
    int AcceptanceCount,
    int RejectionCount,
    string CreatedByMemberName,
    string CreatedByMemberRole,
    DateTime CreatedAtUtc);

public sealed record SuggestionWithVoteResponse(
    Guid Id,
    string Title,
    string Description,
    string Status,
    int AcceptanceCount,
    int RejectionCount,
    bool? CurrentUserVote,
    string CreatedByMemberName,
    string CreatedByMemberRole,
    DateTime CreatedAtUtc);

public sealed class SuggestionVoteRequest
{
    [Required]
    public bool IsAcceptance { get; init; }
}

public sealed record SuggestionStatusChangeRequest(string NewStatus);
