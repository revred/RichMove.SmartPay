using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using SmartPay.Core.Webhooks;

namespace SmartPay.Api.Hosted;

public sealed class WebhookOptions
{
    public bool Enabled { get; init; }
    public string Secret { get; init; } = "replace-me";
    public int MaxAttempts { get; init; } = 6;
}

public sealed class WebhookOutboxWorker(
    IWebhookOutbox outbox,
    IWebhookSigner signer,
    IOptions<WebhookOptions> opts,
    IHttpClientFactory httpFactory,
    ILogger<WebhookOutboxWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var http = httpFactory.CreateClient("webhook");
        var options = opts.Value;
        if (!options.Enabled)
        {
            log.LogInformation("Webhook outbox disabled");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var evt = await outbox.DequeueAsync(stoppingToken);
                if (evt is null)
                {
                    await Task.Delay(250, stoppingToken);
                    continue;
                }

                var sig = signer.Sign(evt.Payload, options.Secret);
                var req = new HttpRequestMessage(HttpMethod.Post, evt.Destination);
                req.Headers.Add("X-SmartPay-Signature", sig);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                req.Content = new StringContent(evt.Payload, Encoding.UTF8, "application/json");

                var res = await http.SendAsync(req, stoppingToken);
                if (!res.IsSuccessStatusCode)
                {
                    var attempts = evt.Attempt + 1;
                    if (attempts < options.MaxAttempts)
                    {
                        var backoffMs = (int)Math.Min(30000, Math.Pow(2, attempts) * 100);
                        await Task.Delay(backoffMs, stoppingToken);
                        await outbox.EnqueueAsync(evt with { Attempt = attempts }, stoppingToken);
                    }
                    else
                    {
                        log.LogError("Webhook delivery failed after {Attempts} attempts for {Id}", attempts, evt.Id);
                    }
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                log.LogError(ex, "Webhook worker error");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}