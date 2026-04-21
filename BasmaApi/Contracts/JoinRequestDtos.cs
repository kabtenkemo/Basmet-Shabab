using System.ComponentModel.DataAnnotations;

namespace BasmaApi.Contracts;

public sealed class TeamJoinRequestCreateRequest
{
    [Required]
    public string FullName { get; init; } = string.Empty;

    [Required]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required, RegularExpression(@"^[0-9]{14}$")]
    public string NationalId { get; init; } = string.Empty;

    public DateOnly? BirthDate { get; init; }

    [Required]
    public Guid GovernorateId { get; init; }

    public string ApplicationType { get; init; } = "GovernorateMembers";

    public Guid? CommitteeId { get; init; }

    [Required]
    public string Motivation { get; init; } = string.Empty;

    public string? Experience { get; init; }
}

public sealed class TeamJoinRequestReviewRequest
{
    public string Status { get; init; } = string.Empty;

    public string? AdminNotes { get; init; }
}

public sealed record TeamJoinRequestResponse(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    string? NationalId,
    DateOnly? BirthDate,
    Guid GovernorateId,
    string GovernorateName,
    Guid? CommitteeId,
    string? CommitteeName,
    string Motivation,
    string? Experience,
    string Status,
    string? AdminNotes,
    Guid? AssignedToMemberId,
    string? AssignedToMemberName,
    Guid? ReviewedByMemberId,
    string? ReviewedByMemberName,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc);
