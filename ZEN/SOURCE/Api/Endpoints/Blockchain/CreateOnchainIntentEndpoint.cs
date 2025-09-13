using FastEndpoints;
using RichMove.SmartPay.Infrastructure.Blockchain;
using RichMove.SmartPay.Infrastructure.Blockchain.Repositories;

namespace RichMove.SmartPay.Api.Endpoints.Blockchain;

public sealed class CreateOnchainIntentRequest
{
    public Guid SourceAssetId { get; init; }
    public Guid TargetAssetId { get; init; }
    public decimal AmountSource { get; init; }
    public string? QuoteId { get; init; }
    public Guid? UserId { get; init; }
    public string? IdempotencyKey { get; init; }
}

public sealed class CreateOnchainIntentResponse
{
    public Guid IntentId { get; init; }
    public string Status { get; init; } = "CREATED";
}

public sealed class CreateOnchainIntentEndpoint : Endpoint<CreateOnchainIntentRequest, CreateOnchainIntentResponse>
{
    private readonly IBlockchainGate _gate;
    private readonly IntentRepository _repo;

    public CreateOnchainIntentEndpoint(IBlockchainGate gate, IntentRepository repo)
    {
        _gate = gate;
        _repo = repo;
    }

    public override void Configure()
    {
        Post("/v1/chain/intents/onchain");
        AllowAnonymous();
        Summary(s => {
            s.Summary = "Create on-chain payment intent";
            s.Description = "Creates a chain-agnostic payment intent (route='ONCHAIN')";
        });
    }

    public override async Task HandleAsync(CreateOnchainIntentRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (!_gate.Enabled)
        {
            await SendAsync(new CreateOnchainIntentResponse(), 501, ct);
            return;
        }

        if (req.AmountSource <= 0)
        {
            AddError(r => r.AmountSource, "AmountSource must be > 0");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var id = await _repo.CreateAsync(req.SourceAssetId, req.TargetAssetId, req.AmountSource, req.QuoteId, req.UserId, "ONCHAIN", "CREATED", req.IdempotencyKey, ct);
        await SendOkAsync(new CreateOnchainIntentResponse { IntentId = id }, ct);
    }
}