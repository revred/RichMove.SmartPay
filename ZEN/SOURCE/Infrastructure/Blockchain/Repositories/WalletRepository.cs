using Npgsql;

namespace RichMove.SmartPay.Infrastructure.Blockchain.Repositories;

public sealed class WalletRepository
{
    private readonly NpgsqlDataSource _db;

    public WalletRepository(NpgsqlDataSource? db) => _db = db!;

    public async Task<Guid> CreateAsync(Guid chainId, string address, Guid? userId, string custody = "EXTERNAL",
        string[]? tags = null, IDictionary<string, object>? metadata = null, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
insert into public.wallet (id, user_id, chain_id, address, custody, tags, metadata)
values (@id, @user_id, @chain_id, @address, @custody, @tags, @metadata)
on conflict (chain_id, address) do nothing;";

        await using var cmd = _db.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("user_id", userId.HasValue ? userId.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("chain_id", chainId);
        cmd.Parameters.AddWithValue("address", address);
        cmd.Parameters.AddWithValue("custody", custody);
        cmd.Parameters.AddWithValue("tags", tags ?? Array.Empty<string>());
        cmd.Parameters.AddWithValue("metadata", (metadata is null) ? "{}" : System.Text.Json.JsonSerializer.Serialize(metadata));
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }
}