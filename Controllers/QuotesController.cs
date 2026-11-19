
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingBackend.Api.Data;
using TradingBackend.Api.DTOs.Quotes;
using TradingBackend.Api.Entities;
using TradingBackend.Api.Fix;
using TradingBackend.Api.Identity;
using QuickFix;

namespace TradingBackend.Api.Controllers;

[Authorize(Roles = RoleConstants.FirmAdmin + "," + RoleConstants.Trader)]
[ApiController]
[Route("api/[controller]")]
public class QuotesController : ControllerBase
{
    private readonly TradingDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly FixQuoteSubmissionService _fixQuoteSubmissionService;

    public QuotesController(
        TradingDbContext db,
        UserManager<ApplicationUser> userManager,
        FixQuoteSubmissionService fixQuoteSubmissionService)
    {
        _db = db;
        _userManager = userManager;
        _fixQuoteSubmissionService = fixQuoteSubmissionService;
    }

    [HttpPost]
    [Authorize(Roles = RoleConstants.Trader)]
    public async Task<IActionResult> CreateSingleQuote(
        [FromBody] CreateSingleQuoteDto dto)
    {
        // 1. Resolve authenticated user.

        var user = await _userManager.GetUserAsync(User);

        if (user == null || !user.IsActive)
            return Unauthorized(new
            {
                message = "User is not active or could not be identified."
            });

        if (!user.FirmId.HasValue)
            return BadRequest(new
            {
                message = "User is not associated with a firm."
            });

        if (string.IsNullOrWhiteSpace(user.CHSEWorkstationId))
            return BadRequest(new
            {
                message = "User does not have a CHSE Workstation ID."
            });

        int firmId = user.FirmId.Value;
        string tradingMnemonic = user.CHSEWorkstationId;

        // 2. Verify firm.

        var firm = await _db.Firms
            .FirstOrDefaultAsync(x =>
                x.Id == firmId &&
                x.IsActive);

        if (firm == null ||
            string.IsNullOrWhiteSpace(firm.CHSEFirmId))
        {
            return BadRequest(new
            {
                message = "The user's firm is inactive or has no CHSE Firm ID."
            });
        }

        // 3. Verify FIX connectivity.

        var activeSession = _fixQuoteSubmissionService.GetActiveSession();

        if (!_fixQuoteSubmissionService.IsConnected ||
            activeSession == null)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message = "FIX session is not connected."
                });
        }

        // 4. Generate unique QuoteMsgID.

        string quoteMsgId = Guid.NewGuid().ToString("N")[..16];

        // 5. Validate and construct the FIX message before persistence.
        // Keep the message outside the try block so it is available
        // after the database transaction commits.

        Message fixMessage;

        try
        {
            fixMessage = _fixQuoteSubmissionService.BuildQuote(
                dto,
                quoteMsgId,
                tradingMnemonic);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }

        // 6. Find or create the logical Quote.
        // Existing duplicate identities must never be selected arbitrarily.

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        bool transactionCommitted = false;

        try
        {
            var matchingQuotes = await _db.Quotes
                .Where(x =>
                    x.FirmId == firmId &&
                    x.TradingMnemonic == tradingMnemonic &&
                    x.Symbol == dto.Symbol &&
                    x.OrderBook == dto.OrderBook)
                .ToListAsync();

            if (matchingQuotes.Count > 1)
            {
                await transaction.RollbackAsync();

                return Conflict(new
                {
                    message =
                        "Multiple logical quotes exist for this firm, " +
                        "trading mnemonic, symbol and order book. " +
                        "Please resolve the duplicate records first."
                });
            }

            Quote quote;

            if (matchingQuotes.Count == 0)
            {
                quote = new Quote
                {
                    FirmId = firmId,
                    TradingMnemonic = tradingMnemonic,
                    Symbol = dto.Symbol,
                    OrderBook = dto.OrderBook,
                    Status = "PendingSubmission",
                    IsActive = false,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };

                _db.Quotes.Add(quote);

                await _db.SaveChangesAsync();
            }
            else
            {
                quote = matchingQuotes[0];
            }

            // 7. Create the FIX submission record.

            var submission = new QuoteSubmission
            {
                FirmId = firmId,
                SessionId = activeSession.ToString(),
                MsgType = "S",
                QuoteID = "1",
                QuoteMsgID = quoteMsgId,
                Account = dto.Account,
                SubmissionStatus = "PendingSubmission",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            _db.QuoteSubmissions.Add(submission);

            await _db.SaveChangesAsync();

            // 8. Create the quote revision.

            var revision = new QuoteEntryRevision
            {
                QuoteId = quote.Id,
                QuoteSubmissionId = submission.Id,
                Symbol = dto.Symbol,

                // Bid side
                BidPx = dto.BidPx,
                BidSize = dto.BidSize,

                // Offer side
                OfferPx = dto.OfferPx,
                OfferSize = dto.OfferSize,

                // Additional quote parameters
                MinQty = dto.MinQty,
                ValidUntilTime = dto.ValidUntilTime,
                ExecInst = dto.ExecInst,
                PreTradeAnonymity = dto.PreTradeAnonymity,
                QuoteQualifier = dto.QuoteQualifier,

                Status = "PendingSubmission",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            _db.QuoteEntryRevisions.Add(revision);

            quote.LatestSubmissionId = submission.Id;
            quote.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            // 9. Commit before sending the FIX message.

            await transaction.CommitAsync();
            transactionCommitted = true;

            // 10. Submit the already-built FIX message to QuickFIX/n.

            try
            {
                _fixQuoteSubmissionService.SubmitQuote(fixMessage);

                submission.SubmissionStatus = "SubmittedToFix";
                submission.SubmittedAtUtc = DateTimeOffset.UtcNow;

                await _db.SaveChangesAsync();

                return Accepted(new
                {
                    message = "Quote submitted to FIX session.",
                    quoteId = quote.Id,
                    submissionId = submission.Id,
                    revisionId = revision.Id,
                    quoteMsgId,
                    quoteIdOnExchange = submission.QuoteID,
                    submissionStatus = submission.SubmissionStatus
                });
            }
            catch
            {
                // The database records already exist.
                // Do not delete them if the transport result is uncertain.

                submission.SubmissionStatus = "SubmissionUnknown";

                await _db.SaveChangesAsync();

                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        message =
                            "Quote submission could not be confirmed by " +
                            "the local FIX transport. Exchange acceptance " +
                            "has not been established.",
                        quoteId = quote.Id,
                        submissionId = submission.Id,
                        revisionId = revision.Id,
                        quoteMsgId,
                        submissionStatus = submission.SubmissionStatus
                    });
            }
        }
        catch
        {
            // Roll back only if the transaction has not committed.
            // Never attempt to roll back after the FIX submission stage.

            if (!transactionCommitted)
                await transaction.RollbackAsync();

            throw;
        }
    }
}
