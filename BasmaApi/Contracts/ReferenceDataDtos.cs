namespace BasmaApi.Contracts;

public sealed record GovernorateResponse(
    Guid GovernorateId,
    string Name);

public sealed record CommitteeResponse(
    Guid CommitteeId,
    Guid GovernorateId,
    string GovernorateName,
    string Name,
    DateTime CreatedAtUtc);

public sealed class CommitteeCreateRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(120)]
    public string Name { get; init; } = string.Empty;
}