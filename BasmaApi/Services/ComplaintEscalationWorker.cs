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
        _logger.LogInformation(
            "Complaint escalation worker started with interval {IntervalMinutes} minutes.",
            _interval.TotalMinutes);

        using var timer = new PeriodicTimer(_interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Complaint escalation worker cycle failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }

        _logger.LogInformation("Complaint escalation worker stopped.");
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var escalationService = scope.ServiceProvider.GetRequiredService<IComplaintEscalationService>();

        var complaints = await escalationService.GetEscalationCandidatesAsync(cancellationToken);
        if (complaints.Count == 0)
        {
            _logger.LogDebug("Complaint escalation cycle found no candidates.");
            return;
        }

        var escalatedCount = 0;
        foreach (var complaint in complaints)
        {
            if (complaint.EscalationLevel >= 3)
            {
                continue;
            }

            await escalationService.EscalateAsync(complaint, null, "Auto escalation due to SLA breach.", automatic: true, cancellationToken);
            escalatedCount++;
        }

        _logger.LogInformation(
            "Complaint escalation cycle completed. Candidates={CandidateCount}, Escalated={EscalatedCount}",
            complaints.Count,
            escalatedCount);
    }
}