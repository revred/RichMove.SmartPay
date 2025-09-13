using RichMove.SmartPay.Core.ForeignExchange;

namespace RichMove.SmartPay.Infrastructure.ForeignExchange;

public interface IFxQuoteRepository
{
    Task SaveAsync(FxQuoteResult quote, Guid? createdBy = null, CancellationToken ct = default);
}