namespace BasmaApi.Contracts;

public sealed record GovernorateResponse(
    Guid GovernorateId,
    string Name,
    bool IsVisibleInJoinForm);

public sealed class GovernorateJoinVisibilityRequest
{
    public bool IsVisibleInJoinForm { get; init; }
}

public sealed record CommitteeResponse(
    Guid CommitteeId,
    Guid GovernorateId,
    string GovernorateName,
    string Name,
    bool IsStudentClub,
    bool IsVisibleInJoinForm,
    DateTime CreatedAtUtc);

public sealed class CommitteeJoinVisibilityRequest
{
    public bool IsVisibleInJoinForm { get; init; }
}

public sealed class CommitteeCreateRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    public bool IsStudentClub { get; init; }
}