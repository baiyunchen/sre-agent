using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SreAgent.Repository.Repositories;

namespace SreAgent.Infrastructure.BackgroundJobs;

public class DataCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataCleanupService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public DataCleanupService(IServiceScopeFactory scopeFactory, ILogger<DataCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataCleanupService started, running every {Interval}", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data cleanup failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    internal async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var diagnosticDataRepo = scope.ServiceProvider.GetRequiredService<IDiagnosticDataRepository>();

        var deletedCount = await diagnosticDataRepo.DeleteExpiredAsync(ct);
        if (deletedCount > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired diagnostic data records", deletedCount);
        }
    }
}
