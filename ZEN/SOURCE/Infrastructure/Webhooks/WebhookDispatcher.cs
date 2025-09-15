using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPay.Core.Notifications;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace SmartPay.Infrastructure.Webhooks;

internal interface IWebhookQueue
{
    void Enqueue(WebhookEnvelope envelope);
    bool TryDequeue(out WebhookEnvelope envelope);
}

internal sealed class InMemoryWebhookQueue : IWebhookQueue
{
    private readonly ConcurrentQueue<WebhookEnvelope> _queue = new();

    public void Enqueue(WebhookEnvelope envelope) => _queue.Enqueue(envelope);
    public bool TryDequeue(out WebhookEnvelope envelope) => _queue.TryDequeue(out envelope!);
}

internal sealed class WebhookDeliveryService(
    ILogger<WebhookDeliveryService> logger,
    IConfiguration cfg,
    IWebhookQueue queue,
    IHttpClientFactory httpClientFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = new Wp5Options(cfg);
        var endpoints = ReadEndpoints(cfg);
        var client = httpClientFactory.CreateClient(nameof(WebhookDeliveryService));
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 2, 30));

        logger.LogInformation("WebhookDeliveryService started: {Count} active endpoints", endpoints.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!queue.TryDequeue(out var env))
                {
                    await Task.Delay(200, stoppingToken);
                    continue;
                }

                var json = JsonSerializer.Serialize(env.Payload);

                foreach (var ep in endpoints.Where(e => e.Active))
                {
                    var attempts = 0;
                    var backoff = options.InitialBackoffMs;
                    while (attempts < Math.Max(1, options.MaxAttempts) && !stoppingToken.IsCancellationRequested)
                    {
                        attempts++;
                        try
                        {
                            var sig = WebhookSigner.ComputeSignature(ep.Secret, env.TimestampUnix, json);
                            using var req = new HttpRequestMessage(HttpMethod.Post, ep.Url)
                            {
                                Content = new StringContent(json, Encoding.UTF8, WebhookHeaders.ContentType)
                            };
                            req.Headers.TryAddWithoutValidation(WebhookHeaders.Signature, sig);
                            req.Headers.TryAddWithoutValidation("X-Richmove-Topic", env.Topic);
                            req.Headers.TryAddWithoutValidation("X-Richmove-Tenant", env.TenantId);

                            var res = await client.SendAsync(req, stoppingToken);
                            if ((int)res.StatusCode >= 200 && (int)res.StatusCode < 300)
                            {
                                logger.LogInformation("Webhook delivered to {Name} ({Url}) topic={Topic} tenant={Tenant}",
                                    ep.Name, ep.Url, env.Topic, env.TenantId);
                                break;
                            }

                            logger.LogWarning("Webhook attempt {Attempt} failed ({Status}) to {Name}: {Url}",
                                attempts, (int)res.StatusCode, ep.Name, ep.Url);
                        }
                        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                        {
                            logger.LogWarning(ex, "Webhook exception on attempt {Attempt} to {Name}", attempts, ep.Name);
                        }

                        if (attempts < options.MaxAttempts)
                            await Task.Delay(backoff, stoppingToken);

                        backoff = Math.Min(backoff * 2, 5000);
                    }
                }
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "WebhookDeliveryService loop error");
            }
        }

        logger.LogInformation("WebhookDeliveryService stopping");
    }

    private static List<WebhookEndpoint> ReadEndpoints(IConfiguration cfg)
    {
        var list = new List<WebhookEndpoint>();
        cfg.GetSection("WP5:Webhooks:Endpoints").Bind(list);
        return list;
    }
}

/// <summary>
/// Decorator that forwards notifications to the existing provider (SignalR) and enqueues a webhook delivery.
/// </summary>
internal sealed class CompositeNotificationService(INotificationService inner, IWebhookQueue queue) : INotificationService
{
    public async Task PublishAsync(string tenantId, string topic, object payload, CancellationToken ct = default)
    {
        await inner.PublishAsync(tenantId, topic, payload, ct);

        // Enqueue webhook envelope (serialize payload to JsonElement)
        var json = JsonSerializer.SerializeToElement(payload);
        var env = new WebhookEnvelope(tenantId, topic, json, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        queue.Enqueue(env);
    }
}

public static class WebhookRegistration
{
    public static IServiceCollection Add(this IServiceCollection services, IConfiguration cfg)
    {
        // Required plumbing
        services.AddHttpClient(nameof(WebhookDeliveryService));
        services.AddSingleton<IWebhookQueue, InMemoryWebhookQueue>();
        services.AddHostedService<WebhookDeliveryService>();

        // Decorate existing INotificationService if present
        services.AddSingleton<INotificationService>(sp =>
        {
            var existing = sp.GetRequiredService<INotificationService>();
            var queue = sp.GetRequiredService<IWebhookQueue>();
            return new CompositeNotificationService(existing, queue);
        });

        return services;
    }
}

public sealed class Wp5Options
{
    public bool WebhooksEnabled { get; init; }
    public int TimeoutSeconds { get; init; }
    public int MaxAttempts { get; init; }
    public int InitialBackoffMs { get; init; }

    public Wp5Options(IConfiguration cfg)
    {
        WebhooksEnabled = cfg.GetValue("WP5:Webhooks:Enabled", false);
        TimeoutSeconds = cfg.GetValue("WP5:Webhooks:TimeoutSeconds", 5);
        MaxAttempts = cfg.GetValue("WP5:Webhooks:MaxAttempts", 5);
        InitialBackoffMs = cfg.GetValue("WP5:Webhooks:InitialBackoffMs", 300);
    }
}