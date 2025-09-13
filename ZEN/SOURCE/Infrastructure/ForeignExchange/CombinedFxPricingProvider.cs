using Microsoft.Extensions.Options;

namespace RichMove.SmartPay.Infrastructure.ForeignExchange;

/// <summary>
/// Uses Supabase pricing if available; otherwise falls back to appsettings.
/// </summary>
public sealed class CombinedFxPricingProvider : IFxPricingProvider
{
    private readonly IFxPricingProvider? _supabase;
    private readonly FxPricingOptions _fallback;

    public CombinedFxPricingProvider(IOptions<FxPricingOptions> fallbackOptions, IEnumerable<IFxPricingProvider> providers)
    {
        _fallback = fallbackOptions.Value;
        _supabase = providers.FirstOrDefault(p => p is SupabasePricingProvider);
    }

    public int MarkupBps => _supabase?.MarkupBps ?? _fallback.MarkupBps;
    public int FixedFeeMinorUnits => _supabase?.FixedFeeMinorUnits ?? _fallback.FixedFeeMinorUnits;
}