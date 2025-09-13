using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RichMove.SmartPay.Api;
using RichMove.SmartPay.Api.Idempotency;
using RichMove.SmartPay.Api.Middleware;
using RichMove.SmartPay.Core.Blockchain;
using RichMove.SmartPay.Infrastructure.Blockchain;

namespace RichMove.SmartPay.Api.Extensions;

public static partial class SmartPayHardeningExtensions
{
    public static IServiceCollection AddSmartPayPlatformHardening(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        services.Configure<FeatureFlags>(config.GetSection("Features"));

        // Idempotency store (in-memory baseline)
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

        return services;
    }

    public static IApplicationBuilder UseSmartPayPlatformHardening(this IApplicationBuilder app, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var logger = loggerFactory.CreateLogger("SmartPay.Platform");

        // ProblemDetails for all errors + not-implemented normalization
        app.UseMiddleware<UnhandledExceptionMiddleware>();

        // Correlation ID
        app.UseMiddleware<CorrelationIdMiddleware>();

        // Idempotency for POST/PUT/PATCH endpoints
        app.UseMiddleware<IdempotencyMiddleware>();

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