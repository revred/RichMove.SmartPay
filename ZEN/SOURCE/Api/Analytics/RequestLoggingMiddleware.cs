using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SmartPay.Api.Analytics;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            var path = context.GetEndpoint()?.DisplayName ?? context.Request.Path.ToString();
            var status = context.Response?.StatusCode ?? 0;

            // Counters
            MetricsRegistry.RequestsTotal.Add(1, new KeyValuePair<string, object?>("route", path),
                                                new KeyValuePair<string, object?>("status", status));
            MetricsRegistry.RequestDurationMs.Record(sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("route", path),
                new KeyValuePair<string, object?>("status", status));

            // Logs
            logger.LogInformation("HTTP {Method} {Path} -> {Status} in {ElapsedMs}ms",
                context.Request.Method, path, status, sw.Elapsed.TotalMilliseconds);
        }
    }
}