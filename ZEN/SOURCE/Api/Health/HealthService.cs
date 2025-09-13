using Microsoft.Extensions.Options;
using RichMove.SmartPay.Api.Idempotency;

namespace RichMove.SmartPay.Api.Health;

public sealed class HealthService : IHealthService
{
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IOptions<FeatureFlags> _featureFlags;

    public HealthService(IIdempotencyStore idempotencyStore, IOptions<FeatureFlags> featureFlags)
    {
        _idempotencyStore = idempotencyStore;
        _featureFlags = featureFlags;
    }

    public async Task<HealthResult> CheckReadinessAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Basic configuration validation
            var flags = _featureFlags.Value;

            // Test idempotency store (internal component, safe to check)
            var testKey = $"health-check-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var testExpiry = DateTime.UtcNow.AddMinutes(1);
            var canStore = await _idempotencyStore.TryPutAsync(testKey, testExpiry, cancellationToken);

            if (!canStore)
            {
                return new HealthResult(false, "degraded", "idempotency.store.unavailable",
                    "Idempotency store is not accepting new keys");
            }

            return new HealthResult(true, "ready");
        }
        catch (OperationCanceledException)
        {
            return new HealthResult(false, "timeout", "health.check.timeout",
                "Health check timed out");
        }
        catch (Exception ex)
        {
            return new HealthResult(false, "error", "health.check.exception",
                $"Health check failed: {ex.GetType().Name}");
        }
    }
}