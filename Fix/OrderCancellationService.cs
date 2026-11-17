
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TradingBackend.Api.Data;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Fix;

public class OrderCancellationService
{
    private readonly TradingDbContext _dbContext;
    private readonly FixOrderSubmissionService _fixOrderSubmissionService;
    private readonly ILogger<OrderCancellationService> _logger;

    public OrderCancellationService(
        TradingDbContext dbContext,
        FixOrderSubmissionService fixOrderSubmissionService,
        ILogger<OrderCancellationService> logger)
    {
        _dbContext = dbContext;
        _fixOrderSubmissionService = fixOrderSubmissionService;
        _logger = logger;
    }

    public async Task<OrderCancellationRequest> CancelOrderAsync(
        long orderId,
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
                $"Order cannot be cancelled in its current status: " +
                $"{order.OrdStatus ?? "Unknown"}.");
        }

        if (string.IsNullOrWhiteSpace(order.ClOrdId) &&
            string.IsNullOrWhiteSpace(order.ExchangeOrderId))
        {
            throw new InvalidOperationException(
                "The order has neither a valid ClOrdID nor an Exchange OrderID.");
        }

        // 4. Check for an existing outstanding cancellation.
        var existingRequest = await _dbContext.OrderCancellationRequests
            .AnyAsync(
                x => x.TradingOrderId == order.Id &&
                     (x.Status == "PendingSubmission" ||
                      x.Status == "SubmittedToFix" ||
                      x.Status == "SubmissionUnknown"),
                cancellationToken);

        if (existingRequest)
        {
            throw new InvalidOperationException(
                "An outstanding cancellation request already exists for this order.");
        }

        // 5. Check for an existing outstanding replacement.
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

        // 6. Generate a globally unique 16 digit cancellation ClOrdID.
        var cancelClOrdId = $"CXL-{Guid.NewGuid():N}"[..16];

        var origClOrdId = order.ClOrdId;
        var exchangeOrderId = order.ExchangeOrderId;

        // 7. Validate the FIX message before persisting the request.
        _fixOrderSubmissionService.BuildOrderCancelRequest(
            cancelClOrdId,
            origClOrdId,
            exchangeOrderId,
            order.Symbol,
            order.Side,
            order.OrderBook,
            chseWorkstationId);

        // 8. Persist the cancellation request and identifier together.
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var cancellationRequest = new OrderCancellationRequest
        {
            TradingOrderId = order.Id,
            SessionId = sessionId,
            ClOrdId = cancelClOrdId,
            OrigClOrdId = origClOrdId,
            Status = "PendingSubmission",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var orderIdentifier = new OrderIdentifier
        {
            TradingOrderId = order.Id,
            SessionId = sessionId,
            ClOrdId = cancelClOrdId,
            OrigClOrdId = origClOrdId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.OrderCancellationRequests.Add(cancellationRequest);
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
                "A cancellation request already exists or the generated ClOrdID conflicts.",
                ex);
        }

        // 9. Submit the cancellation to QuickFIX/n.
        try
        {
            _fixOrderSubmissionService.SubmitOrderCancelRequest(
                cancelClOrdId,
                origClOrdId,
                exchangeOrderId,
                order.Symbol,
                order.Side,
                order.OrderBook,
                chseWorkstationId);

            cancellationRequest.Status = "SubmittedToFix";
            cancellationRequest.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Do not blindly retry an uncertain FIX submission.
            cancellationRequest.Status = "SubmissionUnknown";
            cancellationRequest.SubmissionError = ex.Message;
            cancellationRequest.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Order cancellation submission outcome is unknown. " +
                "OrderId: {OrderId}, CancelClOrdID: {ClOrdId}",
                order.Id,
                cancelClOrdId);
        }

        return cancellationRequest;
    }
}
