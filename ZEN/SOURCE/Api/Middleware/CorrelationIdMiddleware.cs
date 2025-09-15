using RichMove.SmartPay.Api.Constants;

namespace RichMove.SmartPay.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Request.Headers.TryGetValue(HeaderNames.CorrelationId, out var corr) || string.IsNullOrWhiteSpace(corr))
        {
            corr = Guid.NewGuid().ToString("N");
            context.Request.Headers[HeaderNames.CorrelationId] = corr;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderNames.CorrelationId] = corr.ToString();
            return Task.CompletedTask;
        });

        await _next(context);
    }
}