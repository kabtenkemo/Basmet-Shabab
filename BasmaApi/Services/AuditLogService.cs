using System.Text.Json;
using BasmaApi.Data;
using BasmaApi.Models;

namespace BasmaApi.Services;

public interface IAuditLogService
{
    Task RecordAsync(string actionType, string entityName, string? entityId = null, object? oldValues = null, object? newValues = null, CancellationToken cancellationToken = default);

    Task RecordAsync(string actionType, string entityName, Guid? userId, string? userName, string? ipAddress, string? entityId = null, object? oldValues = null, object? newValues = null, CancellationToken cancellationToken = default);
}

public sealed class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _dbContext;
    private readonly IAuditRequestContextAccessor _contextAccessor;

    public AuditLogService(AppDbContext dbContext, IAuditRequestContextAccessor contextAccessor)
    {
        _dbContext = dbContext;
        _contextAccessor = contextAccessor;
    }

    public async Task RecordAsync(string actionType, string entityName, string? entityId = null, object? oldValues = null, object? newValues = null, CancellationToken cancellationToken = default)
    {
        await RecordAsync(actionType, entityName, null, null, null, entityId, oldValues, newValues, cancellationToken);
    }

    public async Task RecordAsync(string actionType, string entityName, Guid? userId, string? userName, string? ipAddress, string? entityId = null, object? oldValues = null, object? newValues = null, CancellationToken cancellationToken = default)
    {
        var context = _contextAccessor.Current;
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? context?.UserId,
            UserName = userName ?? context?.UserName ?? "System",
            ActionType = actionType,
            EntityName = entityName,
            EntityId = entityId,
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues),
            TimestampUtc = DateTime.UtcNow,
            IPAddress = ipAddress ?? context?.IPAddress
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}