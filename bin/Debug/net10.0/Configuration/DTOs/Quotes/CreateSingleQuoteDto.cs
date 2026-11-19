namespace TradingBackend.Api.DTOs.Quotes;

public class CreateSingleQuoteDto
{
    // Instrument identity
    public string Symbol { get; set; } = string.Empty;
    public int OrderBook { get; set; } = 1;

    // Account
    public string? Account { get; set; }

    // Minimum execution quantity
    public decimal? MinQty { get; set; }

    // Bid side
    public decimal? BidPx { get; set; }
    public decimal? BidSize { get; set; }

    // Offer side
    public decimal? OfferPx { get; set; }
    public decimal? OfferSize { get; set; }

    // Optional quote validity
    public DateTimeOffset? ValidUntilTime { get; set; }

    // Optional execution instruction: n or 6
    public string? ExecInst { get; set; }

    // Y = Anonymous, N = Named
    public string? PreTradeAnonymity { get; set; } = "Y";

    // Optional qualifier: O = At the Open
    public string? QuoteQualifier { get; set; }
}