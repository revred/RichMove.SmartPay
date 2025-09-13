using Npgsql;
using RichMove.SmartPay.Core.ForeignExchange;

namespace RichMove.SmartPay.Infrastructure.ForeignExchange;

/// <summary>
/// Fetches mid-market rates from public.fx_rate_mid in Supabase.
/// </summary>
public sealed class SupabaseFxRateSource : IFxRateSource
{
    private readonly NpgsqlDataSource _db;

    public SupabaseFxRateSource(NpgsqlDataSource db) => _db = db;

    public decimal GetMidRate(FxCurrencyPair pair)
    {
        // synchronous wrapper over async for IFxRateSource signature
        return GetMidRateAsync(pair, CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task<decimal> GetMidRateAsync(FxCurrencyPair pair, CancellationToken ct)
    {
        // try direct
        var r = await TryGet(pair.ToString(), ct).ConfigureAwait(false);
        if (r is not null) return r.Value;

        // try inverse
        r = await TryGet($"{pair.Quote}/{pair.Base}", ct).ConfigureAwait(false);
        if (r is not null)
            return Decimal.Round(1m / r.Value, 6, MidpointRounding.AwayFromZero);

        throw new KeyNotFoundException($"Mid-rate not available for {pair}.");
    }

    private async Task<decimal?> TryGet(string key, CancellationToken ct)
    {
        await using var cmd = _db.CreateCommand("select mid_rate from public.fx_rate_mid where pair=@p");
        cmd.Parameters.AddWithValue("p", key);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is null ? null : Convert.ToDecimal(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}