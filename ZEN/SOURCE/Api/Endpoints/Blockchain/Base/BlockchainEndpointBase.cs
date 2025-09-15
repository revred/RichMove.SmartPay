using FastEndpoints;
using RichMove.SmartPay.Infrastructure.Blockchain;

namespace RichMove.SmartPay.Api.Endpoints.Blockchain.Base;

/// <summary>
/// Base for blockchain endpoints. If the feature is disabled, responds with 404 and an explanatory header,
/// so routes effectively "disappear" from the surface area.
/// </summary>
public abstract class BlockchainEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : notnull
{
    private readonly IBlockchainGate _gate;

    protected BlockchainEndpoint(IBlockchainGate gate) => _gate = gate;

    public sealed override async Task HandleAsync(TRequest req, CancellationToken ct)
    {
        if (!_gate.Enabled)
        {
            HttpContext.Response.Headers["X-Feature-Disabled"] = "blockchain";
            await SendNotFoundAsync(ct);
            return;
        }

        await HandleWhenEnabledAsync(req, ct);
    }

    protected abstract Task HandleWhenEnabledAsync(TRequest req, CancellationToken ct);
}