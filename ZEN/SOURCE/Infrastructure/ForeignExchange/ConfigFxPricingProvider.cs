using Microsoft.Extensions.Options;

namespace RichMove.SmartPay.Infrastructure.ForeignExchange;

/// <summary>
/// Configuration-based FX pricing provider that uses values from appsettings.
/// </summary>
public sealed class ConfigFxPricingProvider : IFxPricingProvider
{
    private readonly FxPricingOptions _options;

    public ConfigFxPricingProvider(IOptions<FxPricingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public int MarkupBps => _options.MarkupBps;
    public int FixedFeeMinorUnits => _options.FixedFeeMinorUnits;
}