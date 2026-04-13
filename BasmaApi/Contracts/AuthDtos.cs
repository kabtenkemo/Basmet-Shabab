using System.ComponentModel.DataAnnotations;

namespace BasmaApi.Contracts;

public sealed class RegisterRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress, MaxLength(250)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(250)]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed record AuthResponse(
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
    bool MustChangePassword,
    string Token,
    DateTime ExpiresAtUtc);

public sealed class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed class MemberCreateRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress, MaxLength(250)]
    public string Email { get; init; } = string.Empty;

    [Required, RegularExpression(@"^[0-9]{14}$")]
    public string NationalId { get; init; } = string.Empty;

    [Required]
    public DateOnly? BirthDate { get; init; }

    [Required]
    public string Role { get; init; } = string.Empty;

    public Guid? GovernorateId { get; init; }

    public Guid? CommitteeId { get; init; }
}

public sealed record GrantRoleRequest(string Role);

public sealed record GrantPermissionRequest(string PermissionKey);

public sealed record PointAdjustmentRequest(int Amount, string Reason);