using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RichMove.SmartPay.Api;
using RichMove.SmartPay.Api.Handlers;
using RichMove.SmartPay.Api.Health;
using RichMove.SmartPay.Api.Idempotency;
using RichMove.SmartPay.Api.Middleware;
using RichMove.SmartPay.Api.Monitoring;
using RichMove.SmartPay.Api.Performance;
using RichMove.SmartPay.Api.Resilience;
using RichMove.SmartPay.Api.Security;
using RichMove.SmartPay.Api.Services;
using RichMove.SmartPay.Api.Validation;
using RichMove.SmartPay.Core.Blockchain;
using RichMove.SmartPay.Core.Time;
using RichMove.SmartPay.Infrastructure.Blockchain;

namespace RichMove.SmartPay.Api.Extensions;

public static partial class SmartPayHardeningExtensions
{
    public static IServiceCollection AddSmartPayPlatformHardening(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        services.Configure<FeatureFlags>(config.GetSection("Features"));
        services.Configure<IdempotencyOptions>(config.GetSection("Idempotency"));
        services.Configure<JwtTokenOptions>(config.GetSection("JwtToken"));
        services.Configure<RateLimitOptions>(config.GetSection("RateLimit"));
        services.Configure<CircuitBreakerOptions>(config.GetSection("CircuitBreaker"));
        services.Configure<MemoryPoolOptions>(config.GetSection("MemoryPool"));
        services.Configure<CleanupOptions>(config.GetSection("Cleanup"));

        // Clock abstraction
        services.AddSingleton<IClock, SystemClock>();

        // Health services
        services.AddScoped<IHealthService, HealthService>();

        // Idempotency store (in-memory baseline)
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

        // Correlation ID propagation handler
        services.AddHttpContextAccessor();
        services.AddTransient<CorrelationIdHandler>();

        // Security services
        services.AddSingleton<WebhookSignature>();
        services.AddSingleton<JwtTokenRotator>();
        services.AddSingleton<ClientRateLimiter>();
        services.AddSingleton<AuditLogger>();

        // Validation services (MVP-essential)
        services.AddScoped<AsyncValidationService>();
        services.Configure<InputValidationOptions>(config.GetSection("InputValidation"));

        // Resilience services
        services.AddSingleton<CircuitBreakerService>();

        // Observability services
        services.AddSingleton<ColdStartTracker>();
        services.AddSingleton<RateLimitCounters>();

        // Performance services
        services.AddSingleton<MemoryPoolManager>();
        services.AddTransient(typeof(AsyncDataStreamer<>));

        // Background services
        services.AddHostedService<CleanupBackgroundService>();

        return services;
    }

    public static IApplicationBuilder UseSmartPayPlatformHardening(this IApplicationBuilder app, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var logger = loggerFactory.CreateLogger("SmartPay.Platform");

        // ProblemDetails for all errors + not-implemented normalization
        app.UseMiddleware<UnhandledExceptionMiddleware>();

        // Cold-start tracking (early in pipeline)
        app.UseMiddleware<ColdStartMiddleware>();

        // Correlation ID
        app.UseMiddleware<CorrelationIdMiddleware>();

        // Idempotency for POST/PUT/PATCH endpoints
        app.UseMiddleware<IdempotencyMiddleware>();

        // Input validation (MVP-essential)
        app.UseMiddleware<InputValidationMiddleware>();

        // Blockchain binding by feature flag
        var flags = app.ApplicationServices.GetRequiredService<IOptions<FeatureFlags>>().Value;
        var services = app.ApplicationServices;
        var ledger = flags.BlockchainEnabled
            ? services.GetService<IBlockchainLedger>() ?? new InMemoryBlockchainLedger()
            : services.GetService<IBlockchainLedger>() ?? new NullBlockchainLedger();

        Log.BlockchainLedgerBound(logger, flags.BlockchainEnabled, ledger.GetType().Name);
        return app;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "BlockchainEnabled={Flag}; Ledger={LedgerType}")]
        public static partial void BlockchainLedgerBound(ILogger logger, bool flag, string ledgerType);
    }
}