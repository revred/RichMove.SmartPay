namespace RichMove.SmartPay.Infrastructure.ForeignExchange;

/// <summary>
/// Supplies runtime pricing parameters for FX quotes.
/// </summary>
public interface IFxPricingProvider
{
    int MarkupBps { get; }
    int FixedFeeMinorUnits { get; }
}