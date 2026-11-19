
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;
using TradingBackend.Api.DTOs.Quotes;
using TradingBackend.Api.Entities;

namespace TradingBackend.Api.Fix;

public class QuoteStatusReportPersistenceService
{
    private readonly TradingDbContext _dbContext;
    private readonly ILogger<QuoteStatusReportPersistenceService> _logger;

    public QuoteStatusReportPersistenceService(
        TradingDbContext dbContext,
        ILogger<QuoteStatusReportPersistenceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task PersistAsync(
        QuoteStatusReportDto report,
        long fixMessageLogId,
        Guid processingToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(report.QuoteMsgID))
        {
            throw new InvalidOperationException(
                "Cannot correlate QuoteStatusReport because QuoteMsgID is missing.");
        }

        if (string.IsNullOrWhiteSpace(report.QuoteStatus))
        {
            throw new InvalidOperationException(
                "QuoteStatusReport has no QuoteStatus.");
        }

        string exchangeStatus = MapQuoteStatus(report.QuoteStatus);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Verify that this processor still owns the incoming FIX message.
        var fixLog = await _dbContext.FixMessageLogs
            .FirstOrDefaultAsync(
                x => x.Id == fixMessageLogId &&
                     x.ProcessingStatus == "PROCESSING" &&
                     x.ProcessingToken == processingToken,
                cancellationToken);

        if (fixLog == null)
        {
            throw new InvalidOperationException(
                $"FIX message log {fixMessageLogId} was not found or processing ownership was lost.");
        }

        // Idempotency: do not persist the same FIX message twice.
        var alreadyPersisted = await _dbContext.QuoteStatusHistory
            .AnyAsync(
                x => x.FixMessageLogId == fixMessageLogId,
                cancellationToken);

        if (alreadyPersisted)
        {
            _logger.LogInformation(
                "QuoteStatusReport already persisted. FixMessageLogId: {LogId}",
                fixMessageLogId);

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        // Correlate the exchange response to the original single-quote submission.
        var matches = await (
            from submission in _dbContext.QuoteSubmissions
            join revision in _dbContext.QuoteEntryRevisions
                on submission.Id equals revision.QuoteSubmissionId
            join quote in _dbContext.Quotes
                on revision.QuoteId equals quote.Id
            where submission.SessionId == report.SessionId
                  && submission.MsgType == "S"
                  && submission.QuoteMsgID == report.QuoteMsgID
            select new
            {
                Submission = submission,
                Revision = revision,
                Quote = quote
            })
            .ToListAsync(cancellationToken);

        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                $"No single-quote submission found for SessionId '{report.SessionId}' " +
                $"and QuoteMsgID '{report.QuoteMsgID}'.");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Ambiguous QuoteStatusReport correlation. " +
                $"SessionId: '{report.SessionId}', QuoteMsgID: '{report.QuoteMsgID}', " +
                $"Matches: {matches.Count}.");
        }

        var match = matches[0];

        // Determine whether this report is newer than the latest status
        // already recorded for this quote revision.
        var latestRevisionTransactTime =
            await _dbContext.QuoteStatusHistory
                .Where(x =>
                    x.QuoteEntryRevisionId == match.Revision.Id &&
                    x.TransactTime != null)
                .OrderByDescending(x => x.TransactTime)
                .Select(x => x.TransactTime)
                .FirstOrDefaultAsync(cancellationToken);

        bool isNewerForRevision =
            latestRevisionTransactTime == null ||
            report.TransactTime >= latestRevisionTransactTime;

        // Determine whether this report is newer than the latest status
        // already recorded for the logical quote.
        var latestQuoteTransactTime =
            await _dbContext.QuoteStatusHistory
                .Where(x =>
                    x.QuoteId == match.Quote.Id &&
                    x.TransactTime != null)
                .OrderByDescending(x => x.TransactTime)
                .Select(x => x.TransactTime)
                .FirstOrDefaultAsync(cancellationToken);

        bool isNewerForQuote =
            latestQuoteTransactTime == null ||
            report.TransactTime >= latestQuoteTransactTime;

        // Always preserve the exchange response in history.
        var history = new QuoteStatusHistory
        {
            QuoteId = match.Quote.Id,
            QuoteSubmissionId = match.Submission.Id,
            QuoteEntryRevisionId = match.Revision.Id,

            QuoteStatus = report.QuoteStatus,
            QuoteRejectReason = report.QuoteRejectReason,
            MsgType = "AI",
            TransactTime = report.TransactTime,

            FixMessageLogId = fixMessageLogId,

            ApplID = report.ApplID,
            BidID = report.BidID,
            OfferID = report.OfferID,
            Text = report.Text
        };

        _dbContext.QuoteStatusHistory.Add(history);

        // Update the individual revision only when its report is not stale.
        if (isNewerForRevision)
        {
            match.Revision.Status = exchangeStatus;
        }

        // Aggregate quote state is updated only for the latest submission
        // and only when the report is not stale.
        if (match.Quote.LatestSubmissionId == match.Submission.Id &&
            isNewerForQuote)
        {
            match.Quote.Status = exchangeStatus;
            match.Quote.UpdatedAtUtc = DateTimeOffset.UtcNow;

            switch (report.QuoteStatus)
            {
                case "0": // Accepted
                    match.Quote.IsActive = true;
                    break;

                case "6": // Removed from Market
                case "7": // Expired
                case "17": // Cancelled
                    match.Quote.IsActive = false;
                    break;

                case "5": // Rejected
                    // A rejected update must not deactivate a previously
                    // accepted quote that may still be resting in the market.
                    break;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "QuoteStatusReport persisted. LogId: {LogId}, QuoteId: {QuoteId}, " +
            "RevisionId: {RevisionId}, QuoteMsgID: {QuoteMsgID}, " +
            "QuoteStatus: {QuoteStatus}, AggregateUpdated: {AggregateUpdated}",
            fixMessageLogId,
            match.Quote.Id,
            match.Revision.Id,
            report.QuoteMsgID,
            report.QuoteStatus,
            match.Quote.LatestSubmissionId == match.Submission.Id &&
            isNewerForQuote);
    }

    private static string MapQuoteStatus(string quoteStatus)
    {
        return quoteStatus switch
        {
            "0" => "Accepted",
            "5" => "Rejected",
            "6" => "RemovedFromMarket",
            "7" => "Expired",
            "17" => "Cancelled",

            _ => throw new InvalidOperationException(
                $"Unsupported CHSE QuoteStatus value: '{quoteStatus}'.")
        };
    }
}
