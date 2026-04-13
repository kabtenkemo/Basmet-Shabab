namespace BasmaApi.Models;

public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string ActionType { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string? OldValuesJson { get; set; }

    public string? NewValuesJson { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public string? IPAddress { get; set; }
}