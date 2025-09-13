namespace RichMove.SmartPay.Infrastructure.ForeignExchange;

/// <summary>
/// Configuration options for FX pricing parameters.
/// </summary>
public sealed class FxPricingOptions
{
    public int MarkupBps { get; set; } = 25;
    public int FixedFeeMinorUnits { get; set; } = 99;
}