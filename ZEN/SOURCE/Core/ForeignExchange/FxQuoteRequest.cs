namespace RichMove.SmartPay.Core.ForeignExchange;

/// <summary>
/// Request for foreign exchange quote.
/// </summary>
public sealed record FxQuoteRequest
{
    /// <summary>
    /// Source currency code (e.g., "GBP").
    /// </summary>
    public required string FromCurrency { get; init; }

    /// <summary>
    /// Target currency code (e.g., "USD").
    /// </summary>
    public required string ToCurrency { get; init; }

    /// <summary>
    /// Amount to convert.
    /// </summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// Optional quote type (BUY/SELL).
    /// </summary>
    public string? QuoteType { get; init; }
}