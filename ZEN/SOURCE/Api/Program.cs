using RichMove.SmartPay.Core.ForeignExchange;
using RichMove.SmartPay.Core.Integrations;
using RichMove.SmartPay.Infrastructure.Data;
using RichMove.SmartPay.Infrastructure.ForeignExchange;
using RichMove.SmartPay.Infrastructure.Integrations;
using RichMove.SmartPay.Infrastructure.Supabase;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.Extensions.Options;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Configuration binding per ChatGPT review
builder.Services.AddOptions<SupabaseOptions>()
    .Bind(builder.Configuration.GetSection(SupabaseOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(o => o.Url != null && !string.IsNullOrWhiteSpace(o.Key),
              "Supabase Url/Key are required when enabled");

builder.Services.AddOptions<ShopifyOptions>()
    .Bind(builder.Configuration.GetSection(ShopifyOptions.SectionName))
    .ValidateDataAnnotations();

// FX Pricing configuration
builder.Services.Configure<FxPricingOptions>(builder.Configuration.GetSection("FX:Pricing"));

// === WP2: Conditional Supabase integration ===
var supaSection = builder.Configuration.GetSection("Supabase");
var supaEnabled = supaSection.GetValue<bool>("Enabled");

if (supaEnabled)
{
    // Supabase-backed services
    builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
        NpgsqlDataSourceFactory.Create(sp.GetRequiredService<IOptions<SupabaseOptions>>()));

    builder.Services.AddSingleton<IFxRateSource, SupabaseFxRateSource>();
    builder.Services.AddSingleton<SupabasePricingProvider>();
    builder.Services.AddSingleton<IFxPricingProvider>(sp => sp.GetRequiredService<SupabasePricingProvider>());
    builder.Services.AddSingleton<IFxQuoteRepository, SupabaseFxQuoteRepository>();

    // Background pricing refresh service
    builder.Services.AddHostedService<SupabasePricingRefreshService>();
}
else
{
    // Fallback implementations
    builder.Services.AddSingleton<IFxRateSource, InMemoryFxRateSource>();
    builder.Services.AddSingleton<IFxPricingProvider, ConfigFxPricingProvider>();
    builder.Services.AddSingleton<IFxQuoteRepository, NoOpFxQuoteRepository>();
}

// Main FX quote provider (uses above dependencies)
builder.Services.AddSingleton<IFxQuoteProvider, SimpleFxQuoteProvider>();

// DI registrations per ChatGPT review
builder.Services.AddSingleton<IShopifyClient, NullShopifyClient>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // keep Swashbuckle for existing controllers (if any)
builder.Services.AddHealthChecks();

// WP1.2: FastEndpoints bootstrap (coexists with MVC controllers)
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(); // FastEndpoints.Swagger (NSwag) doc+UI

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// WP1.2: enable FastEndpoints
app.UseFastEndpoints();

// WP1.2: enable FE Swagger (served at /swagger and /swagger/index.html by default)
app.UseSwaggerGen();

app.UseHttpsRedirection();

// Health endpoints per ChatGPT review
app.MapGet("/health/live", () => Results.Ok("Healthy"))
    .WithName("LivenessCheck")
    .WithSummary("Liveness check - app is up")
    .WithOpenApi();

app.MapGet("/health/ready", () => Results.Ok("Ready"))
    .WithName("ReadinessCheck")
    .WithSummary("Readiness check - deps ready (always 200 in WP1)")
    .WithOpenApi();

// Legacy endpoint for compatibility
app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new
{
    service = "RichMove.SmartPay",
    version = "1.0.0",
    status = "operational",
    description = "Payment orchestration platform conceived and specified by Ram Revanur"
}))
.WithName("GetServiceInfo")
.WithOpenApi();

// Hello-FX endpoint per ChatGPT review
app.MapPost("/api/fx/quote", async (FxQuoteRequest request, IFxQuoteProvider fxProvider) =>
{
    var result = await fxProvider.GetQuoteAsync(request).ConfigureAwait(false);
    return Results.Ok(result);
})
.WithName("GetFxQuote")
.WithSummary("Get FX quote (WP1: returns sentinel values)")
.WithOpenApi();

// Hello-Shopify endpoint per ChatGPT review
app.MapGet("/api/shop/info", async (IShopifyClient shopifyClient) =>
{
    var shopInfo = await shopifyClient.GetShopInfoAsync().ConfigureAwait(false);
    return Results.Ok(shopInfo);
})
.WithName("GetShopInfo")
.WithSummary("Get shop info (WP1: returns test data)")
.WithOpenApi();

await app.RunAsync().ConfigureAwait(false);

#pragma warning disable CA1515
public partial class Program
{
    protected Program() { }
}
#pragma warning restore CA1515
