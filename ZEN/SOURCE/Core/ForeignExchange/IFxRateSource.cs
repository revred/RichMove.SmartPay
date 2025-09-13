namespace RichMove.SmartPay.Core.ForeignExchange;

/// <summary>
/// Provides mid-market exchange rates for currency pairs.
/// </summary>
public interface IFxRateSource
{
    decimal GetMidRate(FxCurrencyPair pair);
}