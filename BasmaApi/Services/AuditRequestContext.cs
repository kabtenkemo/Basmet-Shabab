using System.Security.Claims;

namespace BasmaApi.Services;

public sealed record AuditRequestContext(
    Guid? UserId,
    string UserName,
    string? IPAddress,
    string? Path,
    string? Method);

public interface IAuditRequestContextAccessor
{
    AuditRequestContext? Current { get; }

    void Set(AuditRequestContext? context);
}

public sealed class AuditRequestContextAccessor : IAuditRequestContextAccessor
{
    private static readonly AsyncLocal<AuditRequestContext?> CurrentContext = new();

    public AuditRequestContext? Current => CurrentContext.Value;

    public void Set(AuditRequestContext? context)
    {
        CurrentContext.Value = context;
    }

    public static AuditRequestContext FromClaims(ClaimsPrincipal user, string? ipAddress, string? path, string? method)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.Name) ?? "System";

        return new AuditRequestContext(
            Guid.TryParse(userIdValue, out var userId) ? userId : null,
            string.IsNullOrWhiteSpace(userName) ? "System" : userName,
            ipAddress,
            path,
            method);
    }
}