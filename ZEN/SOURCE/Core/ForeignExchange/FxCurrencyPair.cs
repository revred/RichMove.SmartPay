namespace RichMove.SmartPay.Core.ForeignExchange;

/// <summary>
/// Represents a currency pair for foreign exchange operations.
/// </summary>
public readonly record struct FxCurrencyPair(string Base, string Quote)
{
    public override string ToString() => $"{Base}/{Quote}";

    public static FxCurrencyPair Parse(string pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        var parts = pair.Split('/');
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid currency pair format: {pair}");
        return new FxCurrencyPair(parts[0], parts[1]);
    }
}