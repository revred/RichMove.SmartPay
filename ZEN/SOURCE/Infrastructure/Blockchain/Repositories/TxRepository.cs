using Npgsql;

namespace RichMove.SmartPay.Infrastructure.Blockchain.Repositories;

public sealed class TxRepository
{
    private readonly NpgsqlDataSource _db;

    public TxRepository(NpgsqlDataSource? db) => _db = db!;

    public async Task<Guid> IngestTxAsync(Guid chainId, string txHash, Guid? fromWalletId, Guid? toWalletId,
        Guid? assetId, decimal? amount, Guid? feeAssetId, decimal? feeAmount, long? blockNumber,
        string status = "PENDING", IDictionary<string, object>? metadata = null, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
insert into public.onchain_tx
  (id, chain_id, tx_hash, from_wallet_id, to_wallet_id, asset_id, amount, fee_asset_id, fee_amount, block_number, status, first_seen, metadata)
values
  (@id, @chain_id, @tx_hash, @from_wallet_id, @to_wallet_id, @asset_id, @amount, @fee_asset_id, @fee_amount, @block_number, @status, now(), @metadata)
on conflict (chain_id, tx_hash) do nothing;";

        await using var cmd = _db.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("chain_id", chainId);
        cmd.Parameters.AddWithValue("tx_hash", txHash);
        cmd.Parameters.AddWithValue("from_wallet_id", fromWalletId.HasValue ? fromWalletId.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("to_wallet_id", toWalletId.HasValue ? toWalletId.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("asset_id", assetId.HasValue ? assetId.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("amount", amount.HasValue ? amount.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("fee_asset_id", feeAssetId.HasValue ? feeAssetId.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("fee_amount", feeAmount.HasValue ? feeAmount.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("block_number", blockNumber.HasValue ? blockNumber.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("metadata", (metadata is null) ? "{}" : System.Text.Json.JsonSerializer.Serialize(metadata));
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }

    public async Task<Guid> CreateSettlementAsync(Guid intentId, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"insert into public.settlement (id, intent_id, status) values (@id, @intent_id, 'PENDING');";
        await using var cmd = _db.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("intent_id", intentId);
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }

    public async Task<Guid> CreateLegAsync(Guid settlementId, string legType, Guid assetId, decimal amount,
        Guid? walletId, Guid? onchainTxId, IDictionary<string, object>? metadata = null, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
insert into public.settlement_leg
  (id, settlement_id, leg_type, asset_id, amount, wallet_id, onchain_tx_id, metadata)
values
  (@id, @settlement_id, @leg_type, @asset_id, @amount, @wallet_id, @onchain_tx_id, @metadata);";

        await using var cmd = _db.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("settlement_id", settlementId);
        cmd.Parameters.AddWithValue("leg_type", legType);
        cmd.Parameters.AddWithValue("asset_id", assetId);
        cmd.Parameters.AddWithValue("amount", amount);
        cmd.Parameters.AddWithValue("wallet_id", walletId.HasValue ? walletId.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("onchain_tx_id", onchainTxId.HasValue ? onchainTxId.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("metadata", (metadata is null) ? "{}" : System.Text.Json.JsonSerializer.Serialize(metadata));
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }
}