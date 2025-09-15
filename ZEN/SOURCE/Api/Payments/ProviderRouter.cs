using SmartPay.Core.Payments;

namespace SmartPay.Api.Payments;

public interface IProviderRouter
{
    IPaymentProvider Resolve();
}

public sealed class SingleProviderRouter(IEnumerable<IPaymentProvider> providers) : IProviderRouter
{
    private readonly IPaymentProvider _primary = providers.First();
    public IPaymentProvider Resolve() => _primary;
}