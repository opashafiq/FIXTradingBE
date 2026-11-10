namespace TradingBackend.Api.Entities;

public class OrderIdentifier
{
    public long Id { get; set; }

    public long TradingOrderId { get; set; }

    public TradingOrder TradingOrder { get; set; } = null!;

    // FIX session associated with this identifier.
    public string? SessionId { get; set; }

    // Current or previous client order identifier.
    public string ClOrdId { get; set; } = string.Empty;

    // Previous identifier referenced during an amendment.
    public string? OrigClOrdId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;
}