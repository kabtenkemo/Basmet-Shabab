using BasmaApi.Data;
using Microsoft.EntityFrameworkCore;

namespace BasmaApi.Services;

public sealed class ComplaintEscalationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ComplaintEscalationWorker> _logger;
    private readonly TimeSpan _interval;

    public ComplaintEscalationWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<ComplaintEscalationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var minutes = int.TryParse(configuration["Complaints:EscalationIntervalMinutes"], out var parsed) ? parsed : 15;
        _interval = TimeSpan.FromMinutes(Math.Max(1, minutes));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Complaint escalation worker failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var escalationService = scope.ServiceProvider.GetRequiredService<IComplaintEscalationService>();

        var complaints = await escalationService.GetEscalationCandidatesAsync(cancellationToken);
        foreach (var complaint in complaints)
        {
            if (complaint.EscalationLevel >= 3)
            {
                continue;
            }

            await escalationService.EscalateAsync(complaint, null, "Auto escalation due to SLA breach.", automatic: true, cancellationToken);
        }
    }
}