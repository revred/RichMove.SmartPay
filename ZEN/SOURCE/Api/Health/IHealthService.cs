namespace RichMove.SmartPay.Api.Health;

public interface IHealthService
{
    Task<HealthResult> CheckReadinessAsync(CancellationToken cancellationToken = default);
}

public sealed record HealthResult(
    bool IsHealthy,
    string Status,
    string? ReasonCode = null,
    string? Description = null);