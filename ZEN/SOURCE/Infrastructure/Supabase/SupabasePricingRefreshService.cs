using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RichMove.SmartPay.Infrastructure.Data;
using RichMove.SmartPay.Infrastructure.ForeignExchange;

namespace RichMove.SmartPay.Infrastructure.Supabase;

/// <summary>
/// Background refresher for Supabase pricing options so changes are picked up without restarts.
/// Runs only when Supabase is enabled.
/// </summary>
public sealed class SupabasePricingRefreshService : BackgroundService
{
    private readonly SupabasePricingProvider _pricing;
    private readonly ILogger<SupabasePricingRefreshService> _log;
    private readonly TimeSpan _interval;

    public SupabasePricingRefreshService(
        SupabasePricingProvider pricing,
        ILogger<SupabasePricingRefreshService> log,
        IOptions<SupabaseOptions> options)
    {
        _pricing = pricing;
        _log = log;
        // default 5 minutes; can be made configurable if needed
        _interval = TimeSpan.FromMinutes(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("SupabasePricingRefreshService starting");

        // initial fetch
        try
        {
            await _pricing.RefreshAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Initial pricing refresh failed");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
                await _pricing.RefreshAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Periodic pricing refresh failed");
            }
        }

        _log.LogInformation("SupabasePricingRefreshService stopping");
    }
}