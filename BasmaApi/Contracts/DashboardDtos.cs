namespace BasmaApi.Contracts;

public sealed record LeaderboardEntryResponse(
    Guid MemberId,
    string FullName,
    string Role,
    int Points,
    int Rank);

public sealed record DashboardResponse(
    Guid CurrentMemberId,
    string CurrentMemberName,
    string Role,
    int Points,
    int TotalMembers,
    int OpenComplaints,
    IReadOnlyList<LeaderboardEntryResponse> TopMembers);

public sealed record DashboardMeResponse(
    Guid CurrentMemberId,
    string CurrentMemberName,
    string Email,
    string Role,
    string? NationalId,
    DateOnly? BirthDate,
    string? GovernorName,
    string? CommitteeName,
    int Points,
    IReadOnlyList<string> Permissions);