namespace RichMove.SmartPay.Core.ForeignExchange;

/// <summary>
/// Foreign exchange quote provider interface for currency conversion rates.
/// </summary>
public interface IFxQuoteProvider
{
    /// <summary>
    /// Gets an FX quote for converting from one currency to another.
    /// </summary>
    /// <param name="request">The quote request containing currency pair and amount.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>FX quote result.</returns>
    Task<FxQuoteResult> GetQuoteAsync(FxQuoteRequest request, CancellationToken cancellationToken = default);
}