using System.ComponentModel.DataAnnotations;

namespace BasmaApi.Contracts;

public sealed class ImportantContactCreateRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    [Required, MaxLength(40)]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required, MaxLength(120)]
    public string PositionTitle { get; init; } = string.Empty;

    [Required, MaxLength(80)]
    public string Domain { get; init; } = string.Empty;
}

public sealed record ImportantContactResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string PositionTitle,
    string Domain,
    DateTime CreatedAtUtc);
