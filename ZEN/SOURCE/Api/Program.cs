using RichMove.SmartPay.Core.ForeignExchange;
using RichMove.SmartPay.Core.Integrations;
using RichMove.SmartPay.Infrastructure.ForeignExchange;
using RichMove.SmartPay.Infrastructure.Integrations;
using FastEndpoints;
using FastEndpoints.Swagger;

var builder = WebApplication.CreateBuilder(args);

// Basic configuration
builder.Services.AddOptions();

// DI registrations per ChatGPT review
builder.Services.AddSingleton<IFxQuoteProvider, NullFxQuoteProvider>();

// DI registrations per ChatGPT review
builder.Services.AddSingleton<IShopifyClient, NullShopifyClient>();

builder.Services.AddHealthChecks();

// FastEndpoints configuration
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument();

var app = builder.Build();


// Enable FastEndpoints and Swagger
app.UseFastEndpoints();
app.UseSwaggerGen();

app.UseHttpsRedirection();

// Health endpoints per ChatGPT review
app.MapGet("/health/live", () => Results.Ok("Healthy"))
    .WithName("LivenessCheck")
    .WithSummary("Liveness check - app is up");

app.MapGet("/health/ready", () => Results.Ok("Ready"))
    .WithName("ReadinessCheck")
    .WithSummary("Readiness check - deps ready (always 200 in WP1)");

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
