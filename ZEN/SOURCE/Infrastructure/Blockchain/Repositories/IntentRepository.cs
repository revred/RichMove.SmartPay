using Npgsql;

namespace RichMove.SmartPay.Infrastructure.Blockchain.Repositories;

public sealed class IntentRepository : IIntentRepository
{
    private readonly NpgsqlDataSource _db;

    public IntentRepository(NpgsqlDataSource? db) => _db = db!;

    public async Task<Guid> CreateAsync(Guid sourceAssetId, Guid targetAssetId, decimal amountSource,
        string? quoteId, Guid? createdBy, string route = "ONCHAIN", string status = "CREATED",
        string? idempotencyKey = null, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
insert into public.payment_intent
  (id, created_by, source_asset_id, target_asset_id, quote_id, amount_source, amount_target_expected, route, status, idempotency_key, metadata)
values
  (@id, @created_by, @source_asset_id, @target_asset_id, @quote_id, @amount_source, null, @route, @status, @idempotency_key, '{}'::jsonb);";

        await using var cmd = _db.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("created_by", createdBy.HasValue ? createdBy.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("source_asset_id", sourceAssetId);
        cmd.Parameters.AddWithValue("target_asset_id", targetAssetId);
        cmd.Parameters.AddWithValue("quote_id", (object?)quoteId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("amount_source", amountSource);
        cmd.Parameters.AddWithValue("route", route);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("idempotency_key", (object?)idempotencyKey ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }
}