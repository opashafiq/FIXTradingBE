
using System.Globalization;
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;
using TradingBackend.Api.DTOs.Quotes;

namespace TradingBackend.Api.Fix;

public class FixQuoteSubmissionService
{
    private readonly FixApplication _fixApplication;
    private readonly ILogger<FixQuoteSubmissionService> _logger;

    public FixQuoteSubmissionService(
        FixApplication fixApplication,
        ILogger<FixQuoteSubmissionService> logger)
    {
        _fixApplication = fixApplication;
        _logger = logger;
    }

    public bool IsConnected => _fixApplication.IsLoggedOn;

    public SessionID? GetActiveSession()
    {
        if (!IsConnected)
            return null;

        return _fixApplication.ActiveSession;
    }

    public Message BuildQuote(
        CreateSingleQuoteDto quote,
        string quoteMsgId,
        string chseWorkstationId)
    {
        if (quote == null)
            throw new ArgumentNullException(nameof(quote));

        // 1. Validate identifiers.

        if (string.IsNullOrWhiteSpace(quoteMsgId))
            throw new ArgumentException("QuoteMsgID is required.");

        if (quoteMsgId.Length > 128)
            throw new ArgumentException(
                "QuoteMsgID cannot exceed 128 characters.");

        if (string.IsNullOrWhiteSpace(chseWorkstationId))
            throw new ArgumentException(
                "CHSE Workstation ID is required.");

        // 2. Validate instrument identity.

        if (string.IsNullOrWhiteSpace(quote.Symbol))
            throw new ArgumentException("Symbol is required.");

        if (quote.Symbol.Length > 32)
            throw new ArgumentException(
                "Symbol cannot exceed 32 characters.");

        if (quote.OrderBook != 1)
            throw new ArgumentException(
                "Single Quote supports only OrderBook 1 (Regular).");

        // 3. CHSE Single Quote requires BOTH bid and offer sides.

        if (!quote.BidPx.HasValue || !quote.BidSize.HasValue)
            throw new ArgumentException(
                "Both BidPx and BidSize are required for Single Quote.");

        if (!quote.OfferPx.HasValue || !quote.OfferSize.HasValue)
            throw new ArgumentException(
                "Both OfferPx and OfferSize are required for Single Quote.");

        if (quote.BidPx.Value <= 0)
            throw new ArgumentException(
                "BidPx must be greater than zero.");

        if (quote.BidSize.Value <= 0)
            throw new ArgumentException(
                "BidSize must be greater than zero.");

        if (quote.OfferPx.Value <= 0)
            throw new ArgumentException(
                "OfferPx must be greater than zero.");

        if (quote.OfferSize.Value <= 0)
            throw new ArgumentException(
                "OfferSize must be greater than zero.");

        // 4. Validate optional fields.

        if (quote.MinQty.HasValue && quote.MinQty.Value <= 0)
            throw new ArgumentException(
                "MinQty must be greater than zero.");

        if (!string.IsNullOrWhiteSpace(quote.Account) &&
            quote.Account.Length > 64)
        {
            throw new ArgumentException(
                "Account cannot exceed 64 characters.");
        }

        if (quote.ValidUntilTime.HasValue &&
            quote.ValidUntilTime.Value <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException(
                "ValidUntilTime must be in the future.");
        }

        if (!string.IsNullOrWhiteSpace(quote.ExecInst) &&
            quote.ExecInst != "n" &&
            quote.ExecInst != "6")
        {
            throw new ArgumentException(
                "ExecInst must be n or 6.");
        }

        string anonymity =
            string.IsNullOrWhiteSpace(quote.PreTradeAnonymity)
                ? "Y"
                : quote.PreTradeAnonymity;

        if (anonymity != "Y" && anonymity != "N")
        {
            throw new ArgumentException(
                "PreTradeAnonymity must be Y or N.");
        }

        if (!string.IsNullOrWhiteSpace(quote.QuoteQualifier) &&
            quote.QuoteQualifier != "O")
        {
            throw new ArgumentException(
                "QuoteQualifier must be O (At the Open).");
        }

        // 5. Construct FIX Quote (35=S).

        var message = new Message();

        message.Header.SetField(new MsgType("S"));

        // 6. Quote identifiers.

        message.SetField(new StringField(117, "1"));
        message.SetField(new StringField(1166, quoteMsgId));

        // 7. Quote Qualifier repeating group (735 / 695).

        if (!string.IsNullOrWhiteSpace(quote.QuoteQualifier))
        {
            var qualifierGroup = new Group(
                735,
                695,
                new[]
                {
                    695,
                    0
                });

            qualifierGroup.SetField(
                new StringField(695, quote.QuoteQualifier));

            message.AddGroup(qualifierGroup);
        }

        // 8. Trading Mnemonic party (453 / 448 / 447 / 452).

        var workstationParty = new Group(
            453,
            448,
            new[]
            {
                448,
                447,
                452,
                0
            });

        workstationParty.SetField(
            new StringField(448, chseWorkstationId));

        workstationParty.SetField(
            new CharField(447, 'D'));

        workstationParty.SetField(
            new IntField(452, 53));

        message.AddGroup(workstationParty);

        // 9. Instrument identity.

        message.SetField(new StringField(55, quote.Symbol));

        // CHSE custom OrderBook tag.
        // Single Quote supports Regular (1).
        message.SetField(new IntField(30001, quote.OrderBook));

        // 10. Account.

        if (!string.IsNullOrWhiteSpace(quote.Account))
        {
            message.SetField(
                new StringField(1, quote.Account));
        }

        // 11. Minimum quantity.

        if (quote.MinQty.HasValue)
        {
            message.SetField(
                new DecimalField(110, quote.MinQty.Value));
        }

        // 12. Required bid side.

        message.SetField(
            new DecimalField(132, quote.BidPx.Value));

        message.SetField(
            new DecimalField(134, quote.BidSize.Value));

        // 13. Required offer side.

        message.SetField(
            new DecimalField(133, quote.OfferPx.Value));

        message.SetField(
            new DecimalField(135, quote.OfferSize.Value));

        // 14. Quote validity.

        if (quote.ValidUntilTime.HasValue)
        {
            string validUntil = quote.ValidUntilTime.Value
                .UtcDateTime
                .ToString(
                    "yyyyMMdd-HH:mm:ss.ffffff",
                    CultureInfo.InvariantCulture);

            message.SetField(
                new StringField(62, validUntil));
        }

        // 15. Execution instruction.

        if (!string.IsNullOrWhiteSpace(quote.ExecInst))
        {
            message.SetField(
                new StringField(18, quote.ExecInst));
        }

        // 16. PreTradeAnonymity.
        // Retained according to the CHSE Trading Gateway specification.
        // Confirm acceptance against the CHSE test environment because
        // this field is absent from the XML 35=S message definition.

        message.SetField(
            new StringField(1091, anonymity));

        // 17. TransactTime.



        // 18. Logging.

        _logger.LogInformation(
            "Single Quote constructed. QuoteMsgID: {QuoteMsgID}, " +
            "Symbol: {Symbol}, OrderBook: {OrderBook}",
            quoteMsgId,
            quote.Symbol,
            quote.OrderBook);

        _logger.LogInformation(
            "Single Quote FIX message: {Message}",
            message.ToString());

        return message;
    }

    // Sends the exact FIX message previously built and validated.
    public void SubmitQuote(Message message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        _logger.LogInformation(
            "Submitting pre-built Single Quote FIX message: {Message}",
            message.ToString());

        _fixApplication.SendToExchange(message);

        _logger.LogInformation(
            "Single Quote successfully submitted to QuickFIX/n.");
    }

    // Retained for compatibility with any existing callers.
    public void SubmitQuote(
        CreateSingleQuoteDto quote,
        string quoteMsgId,
        string chseWorkstationId)
    {
        var message = BuildQuote(
            quote,
            quoteMsgId,
            chseWorkstationId);

        SubmitQuote(message);
    }
}
