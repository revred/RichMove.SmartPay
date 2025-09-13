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
        // Basic validation
        if (string.IsNullOrWhiteSpace(req.FromCurrency) || req.FromCurrency.Length != 3 ||
            string.IsNullOrWhiteSpace(req.ToCurrency) || req.ToCurrency.Length != 3 ||
            req.Amount <= 0)
        {
            await SendErrorsAsync(400, ct).ConfigureAwait(false);
            return;
        }

        var quote = await _provider.GetQuoteAsync(req, ct).ConfigureAwait(false);
        await SendOkAsync(quote, ct).ConfigureAwait(false);
    }
}
#pragma warning restore CA1812