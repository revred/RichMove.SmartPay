using RichMove.SmartPay.Core.ForeignExchange;

namespace RichMove.SmartPay.Infrastructure.ForeignExchange;

/// <summary>
/// Null implementation of FX quote provider for WP1 testing.
/// Returns sentinel values for testing without real FX service integration.
/// </summary>
public sealed class NullFxQuoteProvider : IFxQuoteProvider
{
    /// <inheritdoc />
    public Task<FxQuoteResult> GetQuoteAsync(FxQuoteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Sentinel rate for testing - always return 1.5 as mock rate
        var result = FxQuoteResult.Success(
            rate: 1.5m,
            convertedAmount: request.Amount * 1.5m
        );

        return Task.FromResult(result);
    }
}