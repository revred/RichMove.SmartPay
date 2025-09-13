using RichMove.SmartPay.Core.ForeignExchange;
using RichMove.SmartPay.Core.Integrations;
using RichMove.SmartPay.Infrastructure.ForeignExchange;
using RichMove.SmartPay.Infrastructure.Integrations;
using RichMove.SmartPay.Infrastructure.Blockchain;
using RichMove.SmartPay.Infrastructure.Blockchain.Repositories;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.Extensions.Logging;
using Npgsql;
using RichMove.SmartPay.Api.Extensions;
using RichMove.SmartPay.Api.Health;

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
builder.Services.SwaggerDocument();

// WP3: Platform Hardening (flags, correlation, idempotency, problem details)
builder.Services.AddSmartPayPlatformHardening(builder.Configuration);

var app = builder.Build();

// WP3: wire middleware & feature-flagged ledger binding
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
app.UseSmartPayPlatformHardening(loggerFactory);

// Enable FastEndpoints and Swagger
app.UseFastEndpoints();
app.UseSwaggerGen();

app.UseHttpsRedirection();

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
