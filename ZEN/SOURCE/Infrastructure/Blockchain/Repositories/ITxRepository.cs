namespace RichMove.SmartPay.Infrastructure.Blockchain.Repositories;

public interface ITxRepository
{
    Task<Guid> IngestTxAsync(Guid chainId, string txHash, Guid? fromWalletId, Guid? toWalletId,
        Guid? assetId, decimal? amount, Guid? feeAssetId, decimal? feeAmount, long? blockNumber,
        string status = "PENDING", IDictionary<string, object>? metadata = null, CancellationToken ct = default);

    Task<Guid> CreateSettlementAsync(Guid intentId, CancellationToken ct = default);

    Task<Guid> CreateLegAsync(Guid settlementId, string legType, Guid assetId, decimal amount,
        Guid? walletId, Guid? onchainTxId, IDictionary<string, object>? metadata = null, CancellationToken ct = default);
}