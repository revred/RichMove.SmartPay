using RichMove.SmartPay.Api.Endpoints.Blockchain.Base;
using RichMove.SmartPay.Infrastructure.Blockchain;
using RichMove.SmartPay.Infrastructure.Blockchain.Repositories;

namespace RichMove.SmartPay.Api.Endpoints.Blockchain;

public sealed class CreateWalletRequest
{
    public Guid ChainId { get; init; }
    public string Address { get; init; } = string.Empty;
    public string Custody { get; init; } = "EXTERNAL"; // or CUSTODIAL

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays",
        Justification = "API request model - array is expected for tags")]
    public string[]? Tags { get; init; }
    public Guid? UserId { get; init; } // optional until auth is wired
}

public sealed class CreateWalletResponse
{
    public Guid WalletId { get; init; }
}

public sealed class CreateWalletEndpoint : BlockchainEndpoint<CreateWalletRequest, CreateWalletResponse>
{
    private readonly IWalletRepository _repo;

    public CreateWalletEndpoint(IBlockchainGate gate, IWalletRepository repo) : base(gate)
    {
        _repo = repo;
    }

    public override void Configure()
    {
        Post("/v1/chain/wallets");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Create wallet record";
            s.Description = "Adds a user or custodial wallet for a given chain/network";
        });
    }

    protected override async Task HandleWhenEnabledAsync(CreateWalletRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (string.IsNullOrWhiteSpace(req.Address))
        {
            AddError(r => r.Address, "Address is required");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var id = await _repo.CreateAsync(req.ChainId, req.Address, req.UserId, req.Custody, req.Tags, null, ct);
        await SendOkAsync(new CreateWalletResponse { WalletId = id }, ct);
    }
}