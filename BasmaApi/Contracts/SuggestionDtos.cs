using System.ComponentModel.DataAnnotations;

namespace BasmaApi.Contracts;

public sealed class SuggestionCreateRequest
{
    [Required, MinLength(3), MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required, MinLength(10), MaxLength(1000)]
    public string Description { get; init; } = string.Empty;
}

public sealed record SuggestionItemResponse(
    Guid SuggestionId,
    string Title,
    string Description,
    string CreatedByName,
    DateTime CreatedAtUtc,
    int AcceptCount,
    int RejectCount,
    bool? UserVote);

public sealed record SuggestionDetailResponse(
    Guid SuggestionId,
    string Title,
    string Description,
    string CreatedByName,
    DateTime CreatedAtUtc,
    int AcceptCount,
    int RejectCount,
    bool? UserVote);
