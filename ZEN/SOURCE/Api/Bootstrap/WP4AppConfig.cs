using SmartPay.Api.Analytics;
using SmartPay.Api.Middleware;
using SmartPay.Api.Triggers;

namespace SmartPay.Api.Bootstrap;

public static class WP4AppConfig
{
    public static IApplicationBuilder UseWp4Features(this IApplicationBuilder app, IConfiguration cfg)
    {
        var wp4 = new Wp4Options(cfg);

        if (wp4.AnalyticsEnabled && wp4.RequestLogging)
            app.UseMiddleware<RequestLoggingMiddleware>();

        if (wp4.MultiTenancyEnabled)
            app.UseMiddleware<TenantMiddleware>();

        if (wp4.TriggersFxQuoteCreated)
            app.UseMiddleware<FxQuoteTriggerMiddleware>();

        // Map hub with endpoint routing
        if (wp4.NotificationsEnabled)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<SmartPay.Api.Notifications.NotificationsHub>("/hubs/notifications");
            });
        }

        return app;
    }

    public static IServiceCollection AddWp4Features(this IServiceCollection services, IConfiguration cfg)
    {
        var wp4 = new Wp4Options(cfg);

        if (wp4.NotificationsEnabled)
        {
            services.AddSignalR();
            SmartPay.Infrastructure.Notifications.NotificationsRegistration.Add(services, cfg);
        }

        if (wp4.MultiTenancyEnabled)
            SmartPay.Infrastructure.MultiTenancy.MultiTenancyRegistration.Add(services, cfg);

        return services;
    }
}