using RichMove.SmartPay.Core.ForeignExchange;

namespace RichMove.SmartPay.Infrastructure.ForeignExchange;

public sealed class NoOpFxQuoteRepository : IFxQuoteRepository
{
    public Task SaveAsync(FxQuoteResult quote, Guid? createdBy = null, CancellationToken ct = default)
        => Task.CompletedTask;
}