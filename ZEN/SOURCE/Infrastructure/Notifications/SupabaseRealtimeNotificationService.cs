using SmartPay.Core.Notifications;

namespace SmartPay.Infrastructure.Notifications;

internal sealed class SupabaseRealtimeNotificationService : INotificationService
{
    public Task PublishAsync(string tenantId, string topic, object payload, CancellationToken ct = default)
    {
        // TODO: publish to Supabase channel: $"tenant::{tenantId}:{topic}"
        return Task.CompletedTask;
    }
}