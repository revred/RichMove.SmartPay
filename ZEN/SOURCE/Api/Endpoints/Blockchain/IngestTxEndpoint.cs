using FastEndpoints;
using RichMove.SmartPay.Api.Endpoints.Blockchain.Base;
using RichMove.SmartPay.Infrastructure.Blockchain;
using RichMove.SmartPay.Infrastructure.Blockchain.Repositories;

namespace RichMove.SmartPay.Api.Endpoints.Blockchain;

public sealed class IngestTxRequest
{
    public Guid ChainId { get; init; }
    public string TxHash { get; init; } = string.Empty;
    public Guid? FromWalletId { get; init; }
    public Guid? ToWalletId { get; init; }
    public Guid? AssetId { get; init; }
    public decimal? Amount { get; init; }
    public Guid? FeeAssetId { get; init; }
    public decimal? FeeAmount { get; init; }
    public long? BlockNumber { get; init; }

    // Optional: immediately link to an intent by creating a settlement+legs.
    public Guid? IntentId { get; init; }
    public Guid? CreditWalletId { get; init; } // for leg
}

public sealed class IngestTxResponse
{
    public Guid TxId { get; init; }
    public Guid? SettlementId { get; init; }
}

public sealed class IngestTxEndpoint : BlockchainEndpoint<IngestTxRequest, IngestTxResponse>
{
    private readonly ITxRepository _repo;

    public IngestTxEndpoint(IBlockchainGate gate, ITxRepository repo) : base(gate)
    {
        _repo = repo;
    }

    public override void Configure()
    {
        Post("/v1/chain/tx/ingest");
        AllowAnonymous();
        Summary(s => {
            s.Summary = "Ingest on-chain transaction";
            s.Description = "Stores on-chain tx and optionally creates a settlement+legs for a payment intent";
        });
    }

    protected override async Task HandleWhenEnabledAsync(IngestTxRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (string.IsNullOrWhiteSpace(req.TxHash))
        {
            AddError(r => r.TxHash, "TxHash is required");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var txId = await _repo.IngestTxAsync(req.ChainId, req.TxHash, req.FromWalletId, req.ToWalletId,
            req.AssetId, req.Amount, req.FeeAssetId, req.FeeAmount, req.BlockNumber, "PENDING", null, ct);

        Guid? settlementId = null;
        if (req.IntentId.HasValue && req.AssetId.HasValue && req.Amount.HasValue)
        {
            settlementId = await _repo.CreateSettlementAsync(req.IntentId.Value, ct);
            // Credit leg to recipient
            await _repo.CreateLegAsync(settlementId.Value, "CREDIT", req.AssetId.Value, req.Amount.Value,
                req.CreditWalletId, txId, null, ct);
            // Optional fee leg
            if (req.FeeAssetId.HasValue && req.FeeAmount.HasValue && req.FeeAmount.Value > 0)
            {
                await _repo.CreateLegAsync(settlementId.Value, "FEE", req.FeeAssetId.Value, req.FeeAmount.Value,
                    null, txId, null, ct);
            }
        }

        await SendOkAsync(new IngestTxResponse { TxId = txId, SettlementId = settlementId }, ct);
    }
}