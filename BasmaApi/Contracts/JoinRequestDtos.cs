namespace BasmaApi.Contracts;

public sealed class TeamJoinRequestCreateRequest
{
    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string? NationalId { get; init; }

    public DateOnly? BirthDate { get; init; }

    public Guid GovernorateId { get; init; }

    public Guid? CommitteeId { get; init; }

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
