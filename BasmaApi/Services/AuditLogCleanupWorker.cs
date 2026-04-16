using BasmaApi.Data;
using Microsoft.EntityFrameworkCore;

namespace BasmaApi.Services;

public sealed class AuditLogCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogCleanupWorker> _logger;

    public AuditLogCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<AuditLogCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit log cleanup worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRunUtc(DateTime.UtcNow);
            if (delay < TimeSpan.FromSeconds(5))
            {
                delay = TimeSpan.FromSeconds(5);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit log cleanup cycle failed.");
            }
        }

        _logger.LogInformation("Audit log cleanup worker stopped.");
    }

    internal static TimeSpan GetDelayUntilNextRunUtc(DateTime utcNow)
    {
        var thisMonthRun = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 10, 0, DateTimeKind.Utc);
        var nextRun = utcNow < thisMonthRun ? thisMonthRun : thisMonthRun.AddMonths(1);
        return nextRun - utcNow;
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var deletedCount = await dbContext.AuditLogs.ExecuteDeleteAsync(cancellationToken);
        _logger.LogInformation("Audit log cleanup completed. Deleted={DeletedCount}", deletedCount);
    }
}
