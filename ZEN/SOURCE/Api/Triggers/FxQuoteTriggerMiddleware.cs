using SmartPay.Core.MultiTenancy;
using SmartPay.Core.Notifications;
using System.Text.Json;

namespace SmartPay.Api.Triggers;

public sealed class FxQuoteTriggerMiddleware(RequestDelegate next, INotificationService notifier, ILogger<FxQuoteTriggerMiddleware> logger)
{
    private const string TargetPath = "/api/fx/quote";

    public async Task Invoke(HttpContext context)
    {
        var isFxQuotePost =
            HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path.Equals(TargetPath, StringComparison.OrdinalIgnoreCase);

        if (!isFxQuotePost)
        {
            await next(context);
            return;
        }

        // Swap response body with a buffer to inspect output JSON
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);

            buffer.Position = 0;
            var status = context.Response.StatusCode;
            if (status >= 200 && status < 300 && buffer.Length > 0)
            {
                using var doc = await JsonDocument.ParseAsync(buffer, cancellationToken: context.RequestAborted);
                var root = doc.RootElement;

                string? quoteId = TryGetString(root, "quoteId") ?? TryGetString(root, "id");
                string? fromCcy = TryGetString(root, "fromCurrency") ?? TryGetString(root, "from");
                string? toCcy = TryGetString(root, "toCurrency") ?? TryGetString(root, "to");
                decimal? amount = TryGetDecimal(root, "amount");
                decimal? rate = TryGetDecimal(root, "rate");

                var tenantId = TenantContext.Current.TenantId;
                var payload = new
                {
                    quoteId,
                    fromCurrency = fromCcy,
                    toCurrency = toCcy,
                    amount,
                    rate,
                    timestampUtc = DateTime.UtcNow
                };

                await notifier.PublishAsync(tenantId, "fx.quote.created", payload, context.RequestAborted);
                logger.LogInformation("Emitted fx.quote.created for tenant {Tenant} (quoteId={QuoteId}, {From}->{To}, amount={Amount}, rate={Rate})",
                    tenantId, quoteId, fromCcy, toCcy, amount, rate);

                // Rewind to copy response out to client
                buffer.Position = 0;
            }
        }
        catch (Exception ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            logger.LogError(ex, "FxQuoteTriggerMiddleware failed to publish notification.");
            buffer.Position = 0;
        }
        finally
        {
            // Copy buffered response to the real body
            await buffer.CopyToAsync(originalBody, context.RequestAborted);
            context.Response.Body = originalBody;
        }
    }

    private static string? TryGetString(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

    private static decimal? TryGetDecimal(JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var e)) return null;
        return e.ValueKind switch
        {
            JsonValueKind.Number when e.TryGetDecimal(out var d) => d,
            JsonValueKind.String when decimal.TryParse(e.GetString(), out var ds) => ds,
            _ => null
        };
    }
}