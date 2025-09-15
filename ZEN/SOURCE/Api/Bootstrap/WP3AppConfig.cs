using SmartPay.Api.Payments;
using SmartPay.Core.Payments;
using SmartPay.Infrastructure.Payments;

namespace SmartPay.Api.Bootstrap;

public static class WP3AppConfig
{
    public static IServiceCollection AddWp3Provider(this IServiceCollection services, IConfiguration cfg)
    {
        // Note: IIdempotencyStore is already registered in SmartPayHardeningExtensions
        services.AddSingleton<MockPayProvider>();
        services.AddSingleton<IPaymentProvider>(sp => sp.GetRequiredService<MockPayProvider>());
        services.AddSingleton<IProviderRouter, SingleProviderRouter>();
        return services;
    }
}