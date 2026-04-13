namespace BasmaApi.Contracts;

public sealed record AuditLogResponse(
    Guid Id,
    Guid? UserId,
    string UserName,
    string ActionType,
    string EntityName,
    string? EntityId,
    string? OldValuesJson,
    string? NewValuesJson,
    DateTime TimestampUtc,
    string? IPAddress);

public sealed record PagedAuditLogResponse(
    IReadOnlyList<AuditLogResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);