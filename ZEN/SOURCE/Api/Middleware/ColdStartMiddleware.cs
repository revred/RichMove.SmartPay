using Microsoft.AspNetCore.Http;
using RichMove.SmartPay.Api.Monitoring;

namespace RichMove.SmartPay.Api.Middleware;

/// <summary>
/// Cold-start tracking middleware
/// Records first request latency for performance monitoring
/// </summary>
public sealed class ColdStartMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ColdStartTracker _coldStartTracker;

    public ColdStartMiddleware(RequestDelegate next, ColdStartTracker coldStartTracker)
    {
        _next = next;
        _coldStartTracker = coldStartTracker;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Process the request
        await _next(context);

        // Record first request completion (only logs once)
        var endpoint = context.Request.Path.Value ?? "unknown";
        var method = context.Request.Method;

        _coldStartTracker.RecordFirstRequest(endpoint, method);
    }
}