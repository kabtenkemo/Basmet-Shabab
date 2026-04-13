using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using BasmaApi.Models;

namespace BasmaApi.Contracts;

public sealed class ComplaintRequest
{
    [Required, MaxLength(200)]
    public string Subject { get; init; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Message { get; init; } = string.Empty;

    public ComplaintPriority? Priority { get; init; }
}

public sealed class ComplaintReviewRequest
{
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ComplaintStatus Status { get; init; }

    [MaxLength(4000)]
    public string? AdminReply { get; init; }
}

public sealed record ComplaintResponse(
    Guid Id,
    Guid MemberId,
    string MemberName,
    string Subject,
    string Message,
    string Status,
    string? AdminReply,
    string Priority,
    int EscalationLevel,
    DateTime LastActionDateUtc,
    Guid? AssignedToMemberId,
    string? AssignedToMemberName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ComplaintHistoryResponse> History);

public sealed record ComplaintListItemResponse(
    Guid Id,
    Guid MemberId,
    string MemberName,
    string Subject,
    string Message,
    string Status,
    string? AdminReply,
    string Priority,
    int EscalationLevel,
    DateTime LastActionDateUtc,
    Guid? AssignedToMemberId,
    string? AssignedToMemberName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record ComplaintHistoryResponse(
    Guid Id,
    Guid ComplaintId,
    string Action,
    Guid? PerformedByUserId,
    string? PerformedByUserName,
    string? Notes,
    DateTime TimestampUtc);

public sealed record ComplaintDetailResponse(
    Guid Id,
    Guid MemberId,
    string MemberName,
    string Subject,
    string Message,
    string Status,
    string? AdminReply,
    string Priority,
    int EscalationLevel,
    DateTime LastActionDateUtc,
    Guid? AssignedToMemberId,
    string? AssignedToMemberName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ComplaintHistoryResponse> History);

public sealed class ComplaintCommentRequest
{
    [Required, MaxLength(4000)]
    public string Notes { get; init; } = string.Empty;
}

public sealed class ComplaintEscalateRequest
{
    [MaxLength(4000)]
    public string? Notes { get; init; }
}