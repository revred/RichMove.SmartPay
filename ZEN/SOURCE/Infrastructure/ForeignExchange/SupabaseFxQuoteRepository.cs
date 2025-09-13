using Npgsql;
using RichMove.SmartPay.Core.ForeignExchange;

namespace RichMove.SmartPay.Infrastructure.ForeignExchange;

/// <summary>
/// Persists quotes into public.fx_quotes.
/// </summary>
public sealed class SupabaseFxQuoteRepository : IFxQuoteRepository
{
    private readonly NpgsqlDataSource _db;

    public SupabaseFxQuoteRepository(NpgsqlDataSource db) => _db = db;

    public async Task SaveAsync(FxQuoteResult quote, Guid? createdBy = null, CancellationToken ct = default)
    {
        const string sql = @"
insert into public.fx_quotes
(quote_id, provider, pair, mid_rate, rate, fee, source_amount, target_amount, expires_at, created_by)
values (@quote_id, @provider, @pair, @mid_rate, @rate, @fee, @source_amount, @target_amount, @expires_at, @created_by)
on conflict (quote_id) do nothing;";

        ArgumentNullException.ThrowIfNull(quote);

        // Use reflection to make this resilient to differing FxQuoteResult shapes
        string quoteId = Get<string>(quote, "QuoteId", "Id", "ID", "QuoteID") ?? Guid.NewGuid().ToString("N");
        string provider = Get<string>(quote, "Provider", "Source", "Issuer") ?? "SimpleFx";
        string pair = Get<string>(quote, "Pair", "CurrencyPair") ?? ComposePairFallback(quote);
        decimal midRate = GetDecimal(quote, "MidRate", "Mid") ?? 0m;
        decimal rate = GetDecimal(quote, "Rate", "Price") ?? 0m;
        decimal fee = GetDecimal(quote, "Fee", "Fees") ?? 0m;
        decimal sourceAmount = GetDecimal(quote, "SourceAmount", "Amount", "InputAmount") ?? 0m;
        decimal targetAmount = GetDecimal(quote, "TargetAmount", "OutputAmount") ?? 0m;
        var expiresAt = GetDateTimeOffset(quote, "ExpiresAt", "Expiry", "ValidUntil") ?? DateTimeOffset.UtcNow.AddMinutes(2);

        await using var cmd = _db.CreateCommand(sql);
        cmd.Parameters.AddWithValue("quote_id", quoteId);
        cmd.Parameters.AddWithValue("provider", provider);
        cmd.Parameters.AddWithValue("pair", pair);
        cmd.Parameters.AddWithValue("mid_rate", midRate);
        cmd.Parameters.AddWithValue("rate", rate);
        cmd.Parameters.AddWithValue("fee", fee);
        cmd.Parameters.AddWithValue("source_amount", sourceAmount);
        cmd.Parameters.AddWithValue("target_amount", targetAmount);
        cmd.Parameters.AddWithValue("expires_at", expiresAt.UtcDateTime);
        cmd.Parameters.AddWithValue("created_by", createdBy.HasValue ? createdBy.Value : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static T? Get<T>(object src, params string[] names)
    {
        foreach (var n in names)
        {
            var p = src.GetType().GetProperty(n);
            if (p is null) continue;
            var v = p.GetValue(src);
            if (v is null) continue;
            try
            {
                if (v is T tv) return tv;
                // best-effort convert
                return (T)Convert.ChangeType(v, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (InvalidCastException) { /* ignore and continue */ }
            catch (FormatException) { /* ignore and continue */ }
        }
        return default;
    }

    private static decimal? GetDecimal(object src, params string[] names)
    {
        foreach (var n in names)
        {
            var p = src.GetType().GetProperty(n);
            if (p is null) continue;
            var v = p.GetValue(src);
            if (v is null) continue;
            if (v is decimal d) return d;
            if (v is double db) return (decimal)db;
            if (v is float f) return (decimal)f;
            if (v is int i) return i;
            if (v is long l) return l;
            if (v is string s && decimal.TryParse(s, out var ds)) return ds;
        }
        return null;
    }

    private static DateTimeOffset? GetDateTimeOffset(object src, params string[] names)
    {
        foreach (var n in names)
        {
            var p = src.GetType().GetProperty(n);
            if (p is null) continue;
            var v = p.GetValue(src);
            if (v is null) continue;
            if (v is DateTimeOffset dto) return dto;
            if (v is DateTime dt) return new DateTimeOffset(dt, TimeSpan.Zero);
            if (v is string s && DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed)) return parsed;
        }
        return null;
    }

    private static string ComposePairFallback(FxQuoteResult q)
    {
        var baseCcy = Get<string>(q, "SourceCurrency", "BaseCurrency", "FromCurrency") ?? "XXX";
        var quoteCcy = Get<string>(q, "TargetCurrency", "QuoteCurrency", "ToCurrency") ?? "YYY";
        return $"{baseCcy}/{quoteCcy}";
    }
}