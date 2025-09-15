using Microsoft.Extensions.Options;
using System.Text.Json;

namespace RichMove.SmartPay.Api.Validation;

/// <summary>
/// Essential input validation middleware for MVP
/// Provides basic security without complex caching dependencies
/// </summary>
public sealed partial class InputValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InputValidationMiddleware> _logger;
    private readonly InputValidationOptions _options;

    public InputValidationMiddleware(
        RequestDelegate next,
        ILogger<InputValidationMiddleware> logger,
        IOptions<InputValidationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!ShouldValidate(context))
        {
            await _next(context);
            return;
        }

        // Validate request size
        if (context.Request.ContentLength > _options.MaxRequestSize)
        {
            Log.RequestTooLarge(_logger, context.Request.ContentLength ?? 0, _options.MaxRequestSize);
            context.Response.StatusCode = 413; // Payload Too Large
            await context.Response.WriteAsync("Request payload too large");
            return;
        }

        // Validate content type for POST/PUT/PATCH
        if (IsModifyingRequest(context) && !IsValidContentType(context))
        {
            Log.InvalidContentType(_logger, context.Request.ContentType ?? "null");
            context.Response.StatusCode = 415; // Unsupported Media Type
            await context.Response.WriteAsync("Unsupported content type");
            return;
        }

        // Basic JSON validation for API endpoints
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) && IsJsonRequest(context))
        {
            if (!await ValidateJsonPayloadAsync(context))
            {
                return; // Response already set
            }
        }

        await _next(context);
    }

    private static bool ShouldValidate(HttpContext context)
    {
        // Skip validation for health checks and static files
        var path = context.Request.Path.Value?.ToUpperInvariant() ?? "";
        return !path.Contains("/HEALTH", StringComparison.Ordinal) &&
               !path.Contains("/SWAGGER", StringComparison.Ordinal) &&
               !path.Contains("/FAVICON", StringComparison.Ordinal);
    }

    private static bool IsModifyingRequest(HttpContext context)
    {
        var method = context.Request.Method;
        return method is "POST" or "PUT" or "PATCH";
    }

    private bool IsValidContentType(HttpContext context)
    {
        var contentType = context.Request.ContentType?.Split(';')[0].Trim().ToUpperInvariant();
        return _options.AllowedContentTypes.Contains(contentType ?? "");
    }

    private static bool IsJsonRequest(HttpContext context)
    {
        return context.Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task<bool> ValidateJsonPayloadAsync(HttpContext context)
    {
        try
        {
            context.Request.EnableBuffering(); // Allow reading body multiple times

            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();

            // Reset stream position for next middleware
            context.Request.Body.Position = 0;

            // Basic JSON syntax validation
            if (!string.IsNullOrEmpty(body))
            {
                try
                {
                    using var document = JsonDocument.Parse(body);

                    // Basic size check
                    if (body.Length > _options.MaxJsonSize)
                    {
                        Log.JsonPayloadTooLarge(_logger, body.Length, _options.MaxJsonSize);
                        context.Response.StatusCode = 413;
                        await context.Response.WriteAsync("JSON payload too large");
                        return false;
                    }

                    // Check for suspiciously deep nesting
                    if (GetJsonDepth(document.RootElement) > _options.MaxJsonDepth)
                    {
                        Log.JsonTooDeep(_logger, _options.MaxJsonDepth);
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsync("JSON structure too complex");
                        return false;
                    }
                }
                catch (JsonException ex)
                {
                    Log.InvalidJsonSyntax(_logger, ex);
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Invalid JSON format");
                    return false;
                }
            }

            Log.JsonValidationPassed(_logger, body.Length);
            return true;
        }
        catch (Exception ex)
        {
            Log.ValidationError(_logger, ex);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal validation error");
            return false;
        }
    }

    private static int GetJsonDepth(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => 1 + (element.EnumerateObject().Any()
                ? element.EnumerateObject().Max(prop => GetJsonDepth(prop.Value))
                : 0),
            JsonValueKind.Array => 1 + (element.EnumerateArray().Any()
                ? element.EnumerateArray().Max(GetJsonDepth)
                : 0),
            _ => 0
        };
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 7301, Level = LogLevel.Warning, Message = "Request too large: {ActualSize} bytes exceeds maximum {MaxSize}")]
        public static partial void RequestTooLarge(ILogger logger, long actualSize, long maxSize);

        [LoggerMessage(EventId = 7302, Level = LogLevel.Warning, Message = "Invalid content type: {ContentType}")]
        public static partial void InvalidContentType(ILogger logger, string contentType);

        [LoggerMessage(EventId = 7303, Level = LogLevel.Warning, Message = "JSON payload too large: {ActualSize} exceeds maximum {MaxSize}")]
        public static partial void JsonPayloadTooLarge(ILogger logger, int actualSize, int maxSize);

        [LoggerMessage(EventId = 7304, Level = LogLevel.Warning, Message = "JSON too deep: exceeds maximum depth {MaxDepth}")]
        public static partial void JsonTooDeep(ILogger logger, int maxDepth);

        [LoggerMessage(EventId = 7305, Level = LogLevel.Warning, Message = "Invalid JSON syntax detected")]
        public static partial void InvalidJsonSyntax(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 7306, Level = LogLevel.Debug, Message = "JSON validation passed: {PayloadSize} bytes")]
        public static partial void JsonValidationPassed(ILogger logger, int payloadSize);

        [LoggerMessage(EventId = 7307, Level = LogLevel.Error, Message = "Validation error occurred")]
        public static partial void ValidationError(ILogger logger, Exception exception);
    }
}

/// <summary>
/// Input validation configuration for MVP
/// </summary>
public sealed class InputValidationOptions
{
    public long MaxRequestSize { get; set; } = 10 * 1024 * 1024; // 10MB
    public int MaxJsonSize { get; set; } = 1024 * 1024; // 1MB
    public int MaxJsonDepth { get; set; } = 32;
    public bool EnableDetailedErrors { get; set; } // For production

    public IReadOnlyList<string> AllowedContentTypes { get; set; } = new[]
    {
        "APPLICATION/JSON",
        "APPLICATION/XML",
        "TEXT/PLAIN",
        "MULTIPART/FORM-DATA",
        "APPLICATION/X-WWW-FORM-URLENCODED"
    };
}