
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TradingBackend.Api.Fix;

public class QuoteExecutionReconciliationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QuoteExecutionReconciliationWorker> _logger;

    private static readonly TimeSpan ReconciliationInterval =
        TimeSpan.FromSeconds(30);

    public QuoteExecutionReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<QuoteExecutionReconciliationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Quote execution reconciliation worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();

                var reconciliationService =
                    scope.ServiceProvider.GetRequiredService<
                        QuoteExecutionReconciliationService>();

                var reconciledCount =
                    await reconciliationService.ReconcilePendingAsync(
                        stoppingToken);
                
                _logger.LogInformation(
                "Periodic quote reconciliation cycle completed. " +
                "ReconciledLegCount={ReconciledLegCount}",
                reconciledCount);

                if (reconciledCount > 0)
                {
                    _logger.LogInformation(
                        "Periodic quote reconciliation completed. " +
                        "ReconciledLegCount={ReconciledLegCount}",
                        reconciledCount);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Periodic quote execution reconciliation failed.");
            }

            try
            {
                await Task.Delay(
                    ReconciliationInterval,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation(
            "Quote execution reconciliation worker stopped.");
    }
}
