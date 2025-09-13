using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RichMove.SmartPay.Api.Constants;
using RichMove.SmartPay.Api.Idempotency;
using RichMove.SmartPay.Core.Time;

namespace RichMove.SmartPay.Api.Middleware;

public sealed class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IIdempotencyStore _store;
    private readonly ILogger<IdempotencyMiddleware> _logger;
    private readonly IdempotencyOptions _options;
    private readonly IClock _clock;

    public IdempotencyMiddleware(RequestDelegate next, IIdempotencyStore store, ILogger<IdempotencyMiddleware> logger, IOptions<IdempotencyOptions> options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        _next = next;
        _store = store;
        _logger = logger;
        _options = options.Value;
        _clock = clock;
    }

    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Method is "POST" or "PUT" or "PATCH")
        {
            var key = context.Request.Headers[HeaderNames.IdempotencyKey].ToString();
            if (string.IsNullOrWhiteSpace(key) || key.Length < 8)
            {
                await WriteProblem(context, "about:blank/idempotency-key-missing", "Idempotency key required", "Provide a unique Idempotency-Key header for writes.", HttpStatusCode.BadRequest);
                return;
            }

            if (await _store.ExistsAsync(key, context.RequestAborted))
            {
                await WriteProblem(context, "about:blank/idempotency-conflict", "Duplicate request", "A request with the same Idempotency-Key was already processed.", HttpStatusCode.Conflict);
                return;
            }

            var ok = await _store.TryPutAsync(key, _clock.UtcNow.AddHours(_options.Hours), context.RequestAborted);
            if (!ok)
            {
                await WriteProblem(context, "about:blank/idempotency-conflict", "Duplicate request", "This key is already in use.", HttpStatusCode.Conflict);
                return;
            }
        }

        await _next(context);
    }

    private static Task WriteProblem(HttpContext ctx, string type, string title, string detail, HttpStatusCode status)
    {
        ctx.Response.ContentType = "application/problem+json";
        ctx.Response.StatusCode = (int)status;
        var payload = new { type, title, status = (int)status, detail, traceId = ctx.TraceIdentifier };
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}