using System.ComponentModel.DataAnnotations;

namespace BasmaApi.Contracts;

public sealed class NewsCreateRequest
{
    [Required, MaxLength(250)]
    public string Title { get; init; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Content { get; init; } = string.Empty;

    [Required]
    public string AudienceType { get; init; } = string.Empty;

    public IReadOnlyList<string>? TargetRoles { get; init; }

    public IReadOnlyList<Guid>? TargetMemberIds { get; init; }
}

public sealed record NewsItemResponse(
    Guid Id,
    string Title,
    string Content,
    string AudienceType,
    Guid CreatedByMemberId,
    string CreatedByName,
    DateTime CreatedAtUtc,
    IReadOnlyList<string> TargetRoles,
    IReadOnlyList<Guid> TargetMemberIds);