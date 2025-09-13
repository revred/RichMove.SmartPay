using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace RichMove.SmartPay.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Request.Headers.TryGetValue(HeaderName, out var corr) || string.IsNullOrWhiteSpace(corr))
        {
            corr = Guid.NewGuid().ToString("N");
            context.Request.Headers[HeaderName] = corr;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = corr.ToString();
            return Task.CompletedTask;
        });

        await _next(context);
    }
}