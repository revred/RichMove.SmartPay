namespace RichMove.SmartPay.Core.ForeignExchange;

/// <summary>
/// Result of foreign exchange quote request.
/// </summary>
public sealed record FxQuoteResult
{
    /// <summary>
    /// Whether the quote was successful.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The exchange rate if successful.
    /// </summary>
    public decimal? Rate { get; init; }

    /// <summary>
    /// Converted amount if successful.
    /// </summary>
    public decimal? ConvertedAmount { get; init; }

    /// <summary>
    /// Error message if unsuccessful.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Quote timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a successful quote result.
    /// </summary>
    public static FxQuoteResult Success(decimal rate, decimal convertedAmount) => new()
    {
        IsSuccess = true,
        Rate = rate,
        ConvertedAmount = convertedAmount
    };

    /// <summary>
    /// Creates a failed quote result.
    /// </summary>
    public static FxQuoteResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage
    };
}