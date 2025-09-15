using FastEndpoints;
using FastEndpoints.Swagger;
using Npgsql;
using RichMove.SmartPay.Api.Extensions;
using RichMove.SmartPay.Api.Health;
using RichMove.SmartPay.Core.ForeignExchange;
using RichMove.SmartPay.Core.Integrations;
using RichMove.SmartPay.Infrastructure.Blockchain;
using RichMove.SmartPay.Infrastructure.Blockchain.Repositories;
using RichMove.SmartPay.Infrastructure.ForeignExchange;
using RichMove.SmartPay.Infrastructure.Integrations;
using SmartPay.Api.Bootstrap;

var builder = WebApplication.CreateBuilder(args);

// Basic configuration
builder.Services.AddOptions();

// DI registrations per ChatGPT review
builder.Services.AddSingleton<IFxQuoteProvider, NullFxQuoteProvider>();

// DI registrations per ChatGPT review
builder.Services.AddSingleton<IShopifyClient, NullShopifyClient>();

builder.Services.AddHealthChecks();

// === Blockchain feature flag and services (isolated) ===
builder.Services.AddSingleton<IBlockchainGate, BlockchainGate>();

var supabaseConnectionString = builder.Configuration.GetConnectionString("Supabase") ??
    builder.Configuration["Supabase:DbConnectionString"];
var blockchainEnabled = string.Equals(builder.Configuration["Blockchain:Enabled"], "true", StringComparison.OrdinalIgnoreCase);

if (!string.IsNullOrEmpty(supabaseConnectionString) && blockchainEnabled)
{
    builder.Services.AddSingleton<NpgsqlDataSource>(provider =>
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(supabaseConnectionString);
        return dataSourceBuilder.Build();
    });

    builder.Services.AddSingleton<IWalletRepository, WalletRepository>();
    builder.Services.AddSingleton<IIntentRepository, IntentRepository>();
    builder.Services.AddSingleton<ITxRepository, TxRepository>();
}
else
{
    // Register stub repositories when blockchain is disabled
    // These won't be called since endpoints check the gate first, but are needed for DI
    builder.Services.AddSingleton<IWalletRepository>(_ => new WalletRepository(null));
    builder.Services.AddSingleton<IIntentRepository>(_ => new IntentRepository(null));
    builder.Services.AddSingleton<ITxRepository>(_ => new TxRepository(null));
}

// FastEndpoints configuration - always register all endpoints
// The blockchain endpoints handle disabled state internally via IBlockchainGate
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.DocumentName = "v1";
        s.Title = "SmartPay API";
        s.Version = "v1";
    };
    o.EnableJWTBearerAuth = false;
    o.ShortSchemaNames = true; // Fix schema ID collisions by using short class names
});

// Add controllers for Group 8 endpoints
builder.Services.AddControllers();

// === CORS policy for Admin ===
var cfg = builder.Configuration;
builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminCors", policy =>
    {
        var origins = cfg.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (origins is { Length: > 0 })
        {
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
        else
        {
            // DEV fallback: allow any localhost:* origin without hardcoding ports
            policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin)) return false;
                try
                {
                    var u = new Uri(origin);
                    return string.Equals(u.Host, "localhost", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
    });
});

// WP3: Platform Hardening (flags, correlation, idempotency, problem details)
builder.Services.AddSmartPayPlatformHardening(builder.Configuration);

// WP4: Advanced Infrastructure (SignalR, multi-tenancy, analytics)
builder.Services.AddWp4Features(builder.Configuration);

// WP5: Event Bridge & Tenant Isolation (webhooks, RLS)
builder.Services.AddWp5Features(builder.Configuration);

// WP3: Payment Provider (minimal mock implementation for E2E demos)
builder.Services.AddWp3Provider(builder.Configuration);

// WP7: Operational Guardrails (admin auth, feature flags, guarded endpoints)
builder.Services.AddWp7Guardrails(builder.Configuration);

var app = builder.Build();

// WP3: wire middleware & feature-flagged ledger binding
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
app.UseSmartPayPlatformHardening(loggerFactory);

// WP4: wire advanced infrastructure middleware
app.UseWp4Features(builder.Configuration);

// WP5: wire event bridge features
app.UseWp5Features(builder.Configuration);

// WP7: wire guardrails (rate limiting, authorization)
app.UseWp7Guardrails(builder.Configuration);

// Enable FastEndpoints and Swagger
app.UseFastEndpoints();
app.UseSwaggerGen();

app.UseHttpsRedirection();

// Add CORS middleware
app.UseRouting();
app.UseCors("AdminCors");

// Health endpoints per ChatGPT review
app.MapGet("/health/live", () => Results.Ok("Healthy"))
    .WithName("LivenessCheck")
    .WithSummary("Liveness check - app is up (no external deps checked)");

app.MapGet("/health/ready", async (IHealthService healthService, CancellationToken ct) =>
{
    var result = await healthService.CheckReadinessAsync(ct);

    if (result.IsHealthy)
    {
        return Results.Ok(new { status = result.Status });
    }

    var response = new
    {
        status = result.Status,
        reasonCode = result.ReasonCode,
        description = result.Description
    };

    return Results.Json(response, statusCode: 503);
})
    .WithName("ReadinessCheck")
    .WithSummary("Readiness check - validates internal components");

// Legacy endpoint for compatibility
app.MapHealthChecks("/health");

// Group 8: Advanced Infrastructure & Deployment endpoints
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    service = "RichMove.SmartPay",
    version = "1.0.0",
    status = "operational",
    description = "Payment orchestration platform conceived and specified by Ram Revanur"
}))
.WithName("GetServiceInfo");


// FX Quote endpoint for integration test compatibility
app.MapPost("/api/fx/quote", async (FxQuoteRequest request, IFxQuoteProvider provider) =>
{
    var quote = await provider.GetQuoteAsync(request).ConfigureAwait(false);
    return Results.Ok(quote);
})
.WithName("PostFxQuote")
.WithSummary("Get FX quote - legacy endpoint");

// Hello-Shopify endpoint per ChatGPT review
app.MapGet("/api/shop/info", async (IShopifyClient shopifyClient) =>
{
    var shopInfo = await shopifyClient.GetShopInfoAsync().ConfigureAwait(false);
    return Results.Ok(shopInfo);
})
.WithName("GetShopInfo")
.WithSummary("Get shop info (WP1: returns test data)");

await app.RunAsync().ConfigureAwait(false);

#pragma warning disable CA1515
public partial class Program
{
    protected Program() { }
}
#pragma warning restore CA1515
