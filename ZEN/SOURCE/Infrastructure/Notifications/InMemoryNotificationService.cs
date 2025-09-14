using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartPay.Core.Notifications;

namespace SmartPay.Infrastructure.Notifications;

internal sealed class InMemoryNotificationService(IHubContext<Hub> hub)
    : INotificationService
{
    public Task PublishAsync(string tenantId, string topic, object payload, CancellationToken ct = default)
        => hub.Clients.Group($"tenant::{tenantId}").SendAsync(topic, payload, ct);
}

public static class NotificationsRegistration
{
    public static IServiceCollection Add(this IServiceCollection services, IConfiguration cfg)
    {
        var provider = cfg.GetValue("WP4:Notifications:Provider", "InMemory")!;
        if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            // Note: InMemoryNotificationService will be resolved at runtime when SignalR hub context is available
            services.AddSingleton<INotificationService>(serviceProvider =>
            {
                // Use object to avoid compile-time reference to Hub
                var hubContext = serviceProvider.GetService<IHubContext<Hub>>();
                return hubContext != null ?
                    new InMemoryNotificationService(hubContext) :
                    new SupabaseRealtimeNotificationService();
            });
        }
        else
        {
            services.AddSingleton<INotificationService, SupabaseRealtimeNotificationService>();
        }
        return services;
    }
}