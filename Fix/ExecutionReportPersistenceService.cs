
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;
using TradingBackend.Api.DTOs.Orders;
using TradingBackend.Api.Entities;
using Npgsql;

namespace TradingBackend.Api.Fix;

public class ExecutionReportPersistenceService
{
    private readonly TradingDbContext _dbContext;
    private readonly ILogger<ExecutionReportPersistenceService> _logger;

    public ExecutionReportPersistenceService(
        TradingDbContext dbContext,
        ILogger<ExecutionReportPersistenceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task PersistAsync(
        ExecutionReportDto report,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(report.ExecId))
        {
            throw new InvalidOperationException(
                "ExecutionReport is missing ExecID (tag 17).");
        }

        // Prevent duplicate ExecutionReports.
        bool alreadyExists = await _dbContext.Executions.AnyAsync(
            x => x.SessionId == report.SessionId &&
                 x.ExecId == report.ExecId,
            cancellationToken);

        if (alreadyExists)
        {
            _logger.LogInformation(
                "Duplicate ExecutionReport ignored. Session: {SessionId}, ExecID: {ExecId}",
                report.SessionId,
                report.ExecId);

            return;
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var order = await _dbContext.OrderIdentifiers
                .Where(x =>
                    x.SessionId == report.SessionId &&
                    (
                        x.ClOrdId == report.ClOrdId ||
                        (
                            !string.IsNullOrWhiteSpace(report.OrigClOrdId) &&
                            x.ClOrdId == report.OrigClOrdId
                        )
                    ))
                .OrderBy(x => x.ClOrdId == report.ClOrdId ? 0 : 1)
                .Select(x => x.TradingOrder)
                .FirstOrDefaultAsync(cancellationToken);

        Execution? referencedExecution = null;

    if (!string.IsNullOrWhiteSpace(report.ExecRefId))
    {
        referencedExecution = await _dbContext.Executions.FirstOrDefaultAsync(
            x => x.SessionId == report.SessionId
                && x.ExecId == report.ExecRefId,
            cancellationToken);

        if (referencedExecution == null)
        {
            _logger.LogWarning(
                "Referenced execution not found. SessionId={SessionId}, ExecRefID={ExecRefId}, ExecID={ExecId}",
                report.SessionId,
                report.ExecRefId,
                report.ExecId);
        }
    }            

        var execution = new Execution
        {
            SessionId = report.SessionId,
            ExecId = report.ExecId,
            ExecRefId = report.ExecRefId,
            ReferencedExecutionId = referencedExecution?.Id,
            ClOrdId = report.ClOrdId,
            ExchangeOrderId = report.OrderId,
            ExecType = report.ExecType ?? string.Empty,
            EventCategory = GetExecutionCategory(report.ExecType),
            OrdStatus = report.OrdStatus ?? string.Empty,
            LastQty = report.LastQty,
            LastPx = report.LastPx,
            CumQty = report.CumQty,
            LeavesQty = report.LeavesQty,
            AvgPx = report.AvgPx,
            Symbol = report.Symbol,
            Side = report.Side,
            OrderBook = report.OrderBook,
            TransactTime = report.TransactTime,
            TradingOrderId = order?.Id,
            MdEntryId = report.MdEntryId,
            OrderCapacity = report.OrderCapacity,

            TrdMatchId = report.TrdMatchId,
            SecondaryTradeId = report.SecondaryTradeId,

            ExecRestatementReason = report.ExecRestatementReason,
            OrdRejReason = report.OrdRejReason,
            
            
        };

        _dbContext.Executions.Add(execution);

        // Register a new client order identifier if this report
        // introduces an identifier not already associated with the order.
        if (order != null &&
            !string.IsNullOrWhiteSpace(report.ClOrdId))
        {
            bool identifierExists = await _dbContext.OrderIdentifiers
                .AnyAsync(
                    x => x.SessionId == report.SessionId &&
                        x.ClOrdId == report.ClOrdId,
                    cancellationToken);

            if (!identifierExists)
            {
                var newIdentifier = new OrderIdentifier
                {
                    TradingOrderId = order.Id,
                    SessionId = report.SessionId,
                    ClOrdId = report.ClOrdId,
                    OrigClOrdId = report.OrigClOrdId,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };

                _dbContext.OrderIdentifiers.Add(newIdentifier);
            }
        }

        if (order != null)
        {
            bool canUpdateOrder =
                report.TransactTime.HasValue &&
                (!order.TransactTime.HasValue ||
                report.TransactTime.Value >= order.TransactTime.Value);

            if (canUpdateOrder)
            {
                order.ClOrdId = report.ClOrdId ?? order.ClOrdId;
                order.ExchangeOrderId = report.OrderId ?? order.ExchangeOrderId;
                order.OrdStatus = report.OrdStatus ?? order.OrdStatus;
                order.LeavesQty = report.LeavesQty ?? order.LeavesQty;
                order.CumQty = report.CumQty ?? order.CumQty;
                order.AvgPx = report.AvgPx ?? order.AvgPx;
                order.TransactTime = report.TransactTime;
                order.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }
            else
            {
                _logger.LogInformation(
                    "Skipping order update because the ExecutionReport is older. " +
                    "ClOrdID={ClOrdId}, ReportTime={ReportTime}, CurrentOrderTime={OrderTime}",
                    report.ClOrdId,
                    report.TransactTime,
                    order.TransactTime);
            }
        }
        else
        {
            _logger.LogWarning(
                "No matching order found for ExecutionReport. SessionId={SessionId}, ClOrdID={ClOrdId}",
                report.SessionId,
                report.ClOrdId);
        }
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (IsDuplicateExecutionException(ex))
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogInformation(
                "Duplicate ExecutionReport detected during concurrent processing. " +
                "SessionId={SessionId}, ExecID={ExecId}",
                report.SessionId,
                report.ExecId);

            return;
        }

        _logger.LogInformation(
            "ExecutionReport persisted. ExecID: {ExecId}, OrderId: {OrderId}",
            report.ExecId,
            order?.Id);
    }


    private static string GetExecutionCategory(string? execType)
    {
        return execType switch
        {
            "F" => "TRADE",
            "G" => "TRADE_CORRECT",
            "H" => "TRADE_CANCEL",
            "0" => "NEW",
            "4" => "CANCELLED",
            "5" => "REPLACED",
            "8" => "REJECTED",
            "9" => "SUSPENDED",
            "A" => "PENDING_NEW",
            "C" => "EXPIRED",
            "D" => "RESTATED",
            "E" => "PENDING_REPLACE",
            "L" => "TRIGGERED",
            _ => "OTHER"
        };
    }    

    private static bool IsDuplicateExecutionException(
    DbUpdateException exception)
{
    var innerException = exception.InnerException;

    while (innerException != null)
    {
        if (innerException is PostgresException postgresException)
        {
            return postgresException.SqlState == "23505"
                && postgresException.ConstraintName ==
                    "IX_executions_SessionId_ExecId";
        }

        innerException = innerException.InnerException;
    }

    return false;
}
}
