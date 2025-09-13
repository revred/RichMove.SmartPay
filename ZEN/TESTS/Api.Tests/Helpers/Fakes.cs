#pragma warning disable CA1515, CA1812
using RichMove.SmartPay.Infrastructure.Blockchain.Repositories;

namespace RichMove.SmartPay.Api.Tests.Helpers;

internal sealed class FakeWalletRepository : IWalletRepository
{
    public Task<Guid> CreateAsync(Guid chainId, string address, Guid? userId, string custody = "EXTERNAL", string[]? tags = null, IDictionary<string, object>? metadata = null, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid());
}

internal sealed class FakeIntentRepository : IIntentRepository
{
    public Task<Guid> CreateAsync(Guid sourceAssetId, Guid targetAssetId, decimal amountSource, string? quoteId, Guid? createdBy, string route = "ONCHAIN", string status = "CREATED", string? idempotencyKey = null, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid());
}

internal sealed class FakeTxRepository : ITxRepository
{
    public Task<Guid> IngestTxAsync(Guid chainId, string txHash, Guid? fromWalletId, Guid? toWalletId, Guid? assetId, decimal? amount, Guid? feeAssetId, decimal? feeAmount, long? blockNumber, string status = "PENDING", IDictionary<string, object>? metadata = null, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid());

    public Task<Guid> CreateSettlementAsync(Guid intentId, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid());

    public Task<Guid> CreateLegAsync(Guid settlementId, string legType, Guid assetId, decimal amount, Guid? walletId, Guid? onchainTxId, IDictionary<string, object>? metadata = null, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid());
}