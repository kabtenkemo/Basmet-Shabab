namespace BasmaApi.Contracts;

public sealed record MemberSummaryResponse(
    Guid MemberId,
    string FullName,
    string Email,
    string Role,
    string? NationalId,
    DateOnly? BirthDate,
    string? GovernorName,
    string? CommitteeName,
    int Points,
    IReadOnlyList<string> Permissions);

public sealed record MemberListItemResponse(
    Guid MemberId,
    string FullName,
    string Email,
    string Role,
    string? NationalId,
    DateOnly? BirthDate,
    string? GovernorName,
    string? CommitteeName,
    int Points,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAtUtc);