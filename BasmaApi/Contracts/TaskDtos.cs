using System.ComponentModel.DataAnnotations;

namespace BasmaApi.Contracts;

public sealed class TaskRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; init; }

    [Required]
    public string AudienceType { get; init; } = string.Empty;

    public IReadOnlyList<string>? TargetRoles { get; init; }

    public IReadOnlyList<Guid>? TargetMemberIds { get; init; }

    public bool IsCompleted { get; init; }

    public DateTime? DueDate { get; init; }
}

public sealed record TaskResponse(
    Guid Id,
    Guid CreatedByMemberId,
    string Title,
    string? Description,
    string AudienceType,
    bool IsCompleted,
    DateTime? DueDate,
    DateTime CreatedAtUtc,
    IReadOnlyList<string> TargetRoles,
    IReadOnlyList<Guid> TargetMemberIds);