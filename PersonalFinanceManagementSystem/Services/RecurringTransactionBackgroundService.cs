using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PersonalFinanceManagementSystem.Services;

/// <summary>
/// Processes due recurring transactions while the application is running.
/// The first pass catches up occurrences missed while the app was stopped.
/// </summary>
public sealed class RecurringTransactionBackgroundService : BackgroundService
{
    private static readonly TimeSpan ProcessingInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringTransactionBackgroundService> _logger;

    public RecurringTransactionBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RecurringTransactionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessDueOccurrences(stoppingToken);

        using var timer = new PeriodicTimer(ProcessingInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessDueOccurrences(stoppingToken);
        }
    }

    private async Task ProcessDueOccurrences(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var recurring = scope.ServiceProvider.GetRequiredService<IRecurringTransactionService>();
            var generated = recurring.GenerateDueOccurrencesForAllUsers();

            if (generated > 0)
            {
                _logger.LogInformation("Generated {Count} recurring transaction occurrence(s).", generated);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Recurring transaction processing failed; it will retry on the next interval.");
        }

        await Task.CompletedTask;
    }
}
