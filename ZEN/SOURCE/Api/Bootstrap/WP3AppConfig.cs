using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartPay.Api.Payments;
using SmartPay.Core.Payments;
using SmartPay.Core.Payments.Idempotency;
using SmartPay.Infrastructure.Payments;

namespace SmartPay.Api.Bootstrap;

public static class WP3AppConfig
{
    public static IServiceCollection AddWp3Provider(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        services.AddSingleton<IPaymentProvider, MockPayProvider>();
        services.AddSingleton<IProviderRouter, SingleProviderRouter>();
        return services;
    }
}