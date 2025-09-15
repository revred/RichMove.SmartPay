using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartPay.Api.Hosted;
using SmartPay.Core.Webhooks;
using SmartPay.Infrastructure.Webhooks;

namespace SmartPay.Api.Bootstrap;

public static class WP5AppConfig
{
    public static IServiceCollection AddWp5Features(this IServiceCollection services, IConfiguration cfg)
    {
        var options = new Wp5Options(cfg);
        if (options.WebhooksEnabled)
        {
            WebhookRegistration.Add(services, cfg);
        }
        return services;
    }

    public static IServiceCollection AddWp5Webhooks(this IServiceCollection services, IConfiguration cfg)
    {
        services.Configure<WebhookOptions>(cfg.GetSection("Webhooks"));
        services.AddHttpClient("webhook");
        services.AddSingleton<IWebhookOutbox, InMemoryOutbox>();
        services.AddSingleton<IWebhookSigner, InMemoryOutbox>();
        services.AddHostedService<WebhookOutboxWorker>();
        return services;
    }

    public static IApplicationBuilder UseWp5Features(this IApplicationBuilder app, IConfiguration cfg)
    {
        // No middleware required for outbound webhooks; background dispatcher started via hosted service.
        return app;
    }
}