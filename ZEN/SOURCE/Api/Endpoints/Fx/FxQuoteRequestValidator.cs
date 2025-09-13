using FastEndpoints;
using FluentValidation;
using RichMove.SmartPay.Core.ForeignExchange;

namespace RichMove.SmartPay.Api.Endpoints.Fx;

/// <summary>
/// Basic validation for FX quote requests.
/// </summary>
#pragma warning disable CA1812
internal sealed class FxQuoteRequestValidator : Validator<FxQuoteRequest>
{
    public FxQuoteRequestValidator()
    {
        RuleFor(x => x.FromCurrency)
            .NotEmpty().WithMessage("From currency is required.")
            .Length(3).WithMessage("From currency must be a 3-letter ISO code.");

        RuleFor(x => x.ToCurrency)
            .NotEmpty().WithMessage("To currency is required.")
            .Length(3).WithMessage("To currency must be a 3-letter ISO code.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("Amount must be greater than zero.");
    }
}
#pragma warning restore CA1812