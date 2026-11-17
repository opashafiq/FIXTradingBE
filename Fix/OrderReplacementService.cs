
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TradingBackend.Api.Data;
using TradingBackend.Api.DTOs.Orders;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Fix;

public class OrderReplacementService
{
    private readonly TradingDbContext _dbContext;
    private readonly FixOrderSubmissionService _fixOrderSubmissionService;
    private readonly ILogger<OrderReplacementService> _logger;

    public OrderReplacementService(
        TradingDbContext dbContext,
        FixOrderSubmissionService fixOrderSubmissionService,
        ILogger<OrderReplacementService> logger)
    {
        _dbContext = dbContext;
        _fixOrderSubmissionService = fixOrderSubmissionService;
        _logger = logger;
    }

    public async Task<OrderReplacementRequest> ReplaceOrderAsync(
        long orderId,
        ReplaceOrderDto dto,
        int firmId,
        string chseWorkstationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chseWorkstationId))
            throw new ArgumentException(
                "CHSE Workstation ID is required.");

        // 1. Check the active FIX session.

        var activeSession =
            _fixOrderSubmissionService.GetActiveSession();

        if (activeSession == null)
            throw new InvalidOperationException(
                "CHSE FIX session is not connected.");

        var sessionId = activeSession.ToString();

        // 2. Load the order within the authenticated firm's scope.

        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(
                x => x.Id == orderId && x.FirmId == firmId,
                cancellationToken);

        if (order == null)
            throw new KeyNotFoundException("Order not found.");

        // 3. Only live, exchange-accepted orders are eligible.

        if (order.OrdStatus != "0" && order.OrdStatus != "1")
        {
            throw new InvalidOperationException(
                $"Order cannot be replaced in its current status: " +
                $"{order.OrdStatus ?? "Unknown"}.");
        }

        if (string.IsNullOrWhiteSpace(order.ClOrdId) &&
            string.IsNullOrWhiteSpace(order.ExchangeOrderId))
        {
            throw new InvalidOperationException(
                "The order has neither a valid ClOrdID nor an Exchange OrderID.");
        }

        if (string.IsNullOrWhiteSpace(order.OrdType))
            throw new InvalidOperationException(
                "The existing order has no valid OrdType.");

        // 4. Prevent simultaneous cancellation and replacement.

        var existingCancellation =
            await _dbContext.OrderCancellationRequests
                .AnyAsync(
                    x => x.TradingOrderId == order.Id &&
                         (x.Status == "PendingSubmission" ||
                          x.Status == "SubmittedToFix" ||
                          x.Status == "SubmissionUnknown"),
                    cancellationToken);

        if (existingCancellation)
        {
            throw new InvalidOperationException(
                "An outstanding cancellation request already exists for this order.");
        }

        // 5. Prevent multiple outstanding replacements.

        var existingReplacement =
            await _dbContext.OrderReplacementRequests
                .AnyAsync(
                    x => x.TradingOrderId == order.Id &&
                         (x.Status == "PendingSubmission" ||
                          x.Status == "SubmittedToFix" ||
                          x.Status == "SubmissionUnknown" ||
                          x.Status == "PendingReplace"),
                    cancellationToken);

        if (existingReplacement)
        {
            throw new InvalidOperationException(
                "An outstanding replacement request already exists for this order.");
        }

        // 6. Resolve effective replacement values.
        // Null DTO properties preserve the existing order values.

        var effectiveOrderQty =
            dto.OrderQty ?? order.OrderQty;

        var effectivePrice =
            dto.Price ?? order.Price;

        var effectiveStopPx =
            dto.StopPx ?? order.StopPx;

        var effectiveTimeInForce =
            string.IsNullOrWhiteSpace(dto.TimeInForce)
                ? order.TimeInForce
                : dto.TimeInForce;

        var effectiveExpireDate =
            effectiveTimeInForce == "6"
                ? dto.ExpireDate ?? order.ExpireDate
                : null;

        // 7. Ensure that at least one order parameter changes.

        bool hasChanges =
            effectiveOrderQty != order.OrderQty ||
            effectivePrice != order.Price ||
            effectiveStopPx != order.StopPx ||
            effectiveTimeInForce != order.TimeInForce ||
            effectiveExpireDate != order.ExpireDate;

        if (!hasChanges)
        {
            throw new InvalidOperationException(
                "No replacement changes were supplied.");
        }

        // 8. Do not reduce total quantity below already executed quantity.

        if (effectiveOrderQty <= 0)
            throw new ArgumentException(
                "OrderQty must be greater than zero.");

        if (effectiveOrderQty < (order.CumQty ?? 0))
        {
            throw new ArgumentException(
                "OrderQty cannot be less than the already executed quantity.");
        }

        // 9. Generate a new replacement ClOrdID.

        var replacementClOrdId =
            $"RPL-{Guid.NewGuid():N}"[..16];

        var origClOrdId = order.ClOrdId;
        var exchangeOrderId = order.ExchangeOrderId;

        // 10. Validate the FIX message before persisting.

        _fixOrderSubmissionService.BuildOrderCancelReplaceRequest(
            replacementClOrdId,
            origClOrdId,
            exchangeOrderId,
            order.Symbol,
            order.Side,
            order.OrderBook,
            order.OrdType,
            effectiveOrderQty,
            effectivePrice,
            effectiveStopPx,
            effectiveTimeInForce,
            effectiveExpireDate,
            chseWorkstationId);

        // 11. Persist replacement request and identifier together.

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var replacementRequest = new OrderReplacementRequest
        {
            TradingOrderId = order.Id,
            SessionId = sessionId,
            ClOrdId = replacementClOrdId,
            OrigClOrdId = origClOrdId ?? string.Empty,
            OrderQty = effectiveOrderQty,
            Price = effectivePrice,
            StopPx = effectiveStopPx,
            TimeInForce = effectiveTimeInForce,
            ExpireDate = effectiveExpireDate,
            Status = "PendingSubmission",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var orderIdentifier = new OrderIdentifier
        {
            TradingOrderId = order.Id,
            SessionId = sessionId,
            ClOrdId = replacementClOrdId,
            OrigClOrdId = origClOrdId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.OrderReplacementRequests.Add(replacementRequest);
        _dbContext.OrderIdentifiers.Add(orderIdentifier);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException pg &&
                  pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);

            throw new InvalidOperationException(
                "A replacement request already exists or the generated ClOrdID conflicts.",
                ex);
        }

        // 12. Submit the replacement to QuickFIX/n.

        try
        {
            _fixOrderSubmissionService.SubmitOrderCancelReplaceRequest(
                replacementClOrdId,
                origClOrdId,
                exchangeOrderId,
                order.Symbol,
                order.Side,
                order.OrderBook,
                order.OrdType,
                effectiveOrderQty,
                effectivePrice,
                effectiveStopPx,
                effectiveTimeInForce,
                effectiveExpireDate,
                chseWorkstationId);

            replacementRequest.Status = "SubmittedToFix";
            replacementRequest.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            replacementRequest.Status = "SubmissionUnknown";
            replacementRequest.SubmissionError = ex.Message;
            replacementRequest.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Order replacement submission outcome is unknown. " +
                "OrderId: {OrderId}, ReplacementClOrdID: {ClOrdId}",
                order.Id,
                replacementClOrdId);
        }

        return replacementRequest;
    }
}
