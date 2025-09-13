namespace RichMove.SmartPay.Infrastructure.Blockchain.Repositories;

public interface IIntentRepository
{
    Task<Guid> CreateAsync(Guid sourceAssetId, Guid targetAssetId, decimal amountSource,
        string? quoteId, Guid? createdBy, string route = "ONCHAIN", string status = "CREATED",
        string? idempotencyKey = null, CancellationToken ct = default);
}