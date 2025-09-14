namespace SmartPay.Core.Notifications;

public interface INotificationService
{
    Task PublishAsync(string tenantId, string topic, object payload, CancellationToken ct = default);
}