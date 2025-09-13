using RichMove.SmartPay.Core.ForeignExchange;
using Microsoft.Extensions.Logging;

namespace RichMove.SmartPay.Infrastructure.ForeignExchange;

/// <summary>
/// First-cut provider that prices quotes from an in-memory mid-rate source.
/// </summary>
public sealed class SimpleFxQuoteProvider : IFxQuoteProvider
{
    private readonly IFxRateSource _rates;
    private readonly IFxPricingProvider _pricing;
    private readonly IFxQuoteRepository _repo;
    private readonly ILogger<SimpleFxQuoteProvider> _log;

    public SimpleFxQuoteProvider(IFxRateSource rates, IFxPricingProvider pricing, IFxQuoteRepository repo, ILogger<SimpleFxQuoteProvider> log)
    {
        _rates = rates;
        _pricing = pricing;
        _repo = repo;
        _log = log;
    }

    public Task<FxQuoteResult> GetQuoteAsync(FxQuoteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pair = FxCurrencyPair.Parse($"{request.FromCurrency}/{request.ToCurrency}");
        var midRate = _rates.GetMidRate(pair);

        var markup = _pricing.MarkupBps / 10000m;
        var rate = midRate * (1 + markup);
        var target = request.Amount * rate;

        var quote = FxQuoteResult.Success(rate, target);

        // Fire-and-forget persist with safe logging (best-effort).
        _ = Task.Run(async () =>
        {
            try
            {
                await _repo.SaveAsync(quote, ct: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Failed to persist FX quote with rate {Rate}", rate);
            }
        }, cancellationToken);

        return Task.FromResult(quote);
    }
}