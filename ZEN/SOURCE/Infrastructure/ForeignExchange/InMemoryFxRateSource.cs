using RichMove.SmartPay.Core.ForeignExchange;

namespace RichMove.SmartPay.Infrastructure.ForeignExchange;

/// <summary>
/// In-memory FX rate source with hardcoded rates for testing/fallback.
/// </summary>
public sealed class InMemoryFxRateSource : IFxRateSource
{
    private static readonly Dictionary<string, decimal> s_rates = new()
    {
        { "USD/GBP", 0.8m },
        { "GBP/USD", 1.25m },
        { "EUR/USD", 1.1m },
        { "USD/EUR", 0.91m },
        { "EUR/GBP", 0.85m },
        { "GBP/EUR", 1.18m }
    };

    public decimal GetMidRate(FxCurrencyPair pair)
    {
        var key = pair.ToString();
        if (s_rates.TryGetValue(key, out var rate))
            return rate;

        // Try inverse
        var inverseKey = $"{pair.Quote}/{pair.Base}";
        if (s_rates.TryGetValue(inverseKey, out var inverseRate))
            return Math.Round(1m / inverseRate, 6, MidpointRounding.AwayFromZero);

        throw new KeyNotFoundException($"Mid-rate not available for {pair}.");
    }
}