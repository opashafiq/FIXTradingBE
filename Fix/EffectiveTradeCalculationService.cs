using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;
using TradingBackend.Api.Fix.Models;

namespace TradingBackend.Api.Fix;

public class EffectiveTradeCalculationService
{
    private readonly TradingDbContext _dbContext;
    private readonly ILogger<EffectiveTradeCalculationService> _logger;

    public EffectiveTradeCalculationService(
        TradingDbContext dbContext,
        ILogger<EffectiveTradeCalculationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<EffectiveTradeCalculationResult> CalculateForOrderAsync(
    long tradingOrderId,
    CancellationToken cancellationToken = default)
{
    var executions = await _dbContext.Executions
        .AsNoTracking()
        .Where(x => x.TradingOrderId == tradingOrderId)
        .OrderBy(x => x.TransactTime)
        .ThenBy(x => x.Id)
        .ToListAsync(cancellationToken);

    _logger.LogInformation(
        "Loaded executions for effective trade calculation. " +
        "TradingOrderId={TradingOrderId}, ExecutionCount={ExecutionCount}",
        tradingOrderId,
        executions.Count);

    // Calculation logic will be added in the next step.
    return new EffectiveTradeCalculationResult();
}


}