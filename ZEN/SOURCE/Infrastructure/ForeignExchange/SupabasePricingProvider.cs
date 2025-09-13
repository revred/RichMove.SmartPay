using Npgsql;

namespace RichMove.SmartPay.Infrastructure.ForeignExchange;

/// <summary>
/// Reads pricing from public.fx_pricing_options in Supabase.
/// Register this only when Supabase is enabled.
/// </summary>
public sealed class SupabasePricingProvider : IFxPricingProvider
{
    private readonly NpgsqlDataSource _db;

    public SupabasePricingProvider(NpgsqlDataSource db) => _db = db;

    public int MarkupBps { get; private set; } = 25;
    public int FixedFeeMinorUnits { get; private set; } = 99;

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select markup_bps, fixed_fee_minor_units from public.fx_pricing_options where id='default'");
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            MarkupBps = reader.GetInt32(0);
            FixedFeeMinorUnits = reader.GetInt32(1);
        }
    }
}