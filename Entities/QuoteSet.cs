
namespace TradingBackend.Api.Entities;

public class QuoteSet
{
    public long Id { get; set; }

    // Parent Mass Quote submission
    public long QuoteSubmissionId { get; set; }

    // FIX quote set identity
    public string QuoteSetID { get; set; } = string.Empty;

    // Optional underlying instrument
    public string? UnderlyingSymbol { get; set; }

    // Quote set validity
    public DateTimeOffset? QuoteSetValidUntilTime { get; set; }

    // Number of quote entries declared in the set
    public int TotNoQuoteEntries { get; set; }

    // FIX LastFragment indicator
    public bool? LastFragment { get; set; }
}
