using FastEndpoints;
using RichMove.SmartPay.Core.ForeignExchange;

namespace RichMove.SmartPay.Api.Endpoints.Fx;

/// <summary>
/// FastEndpoints version of the FX quote endpoint.
/// Replaces the MVC controller route: POST /v1/fx/quotes
/// </summary>
#pragma warning disable CA1812
internal sealed class CreateFxQuoteEndpoint : Endpoint<FxQuoteRequest, FxQuoteResult>
{
    private readonly IFxQuoteProvider _provider;

    public CreateFxQuoteEndpoint(IFxQuoteProvider provider) => _provider = provider;

    public override void Configure()
    {
        Post("/v1/fx/quotes");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Create FX quote";
            s.Description = "Generate an FX quote from mid-rate + configured pricing (markup/fee).";
        });
    }

    public override async Task HandleAsync(FxQuoteRequest req, CancellationToken ct)
    {
        var quote = await _provider.GetQuoteAsync(req, ct).ConfigureAwait(false);
        await SendOkAsync(quote, ct).ConfigureAwait(false);
    }
}
#pragma warning restore CA1812