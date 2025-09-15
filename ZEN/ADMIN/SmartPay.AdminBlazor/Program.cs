using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SmartPay.AdminBlazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient for API calls with fallback logic
builder.Services.AddScoped(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var apiBase = cfg["ApiBaseUrl"];

    // For WebAssembly, if no explicit API base URL is configured,
    // fall back to using the same origin as the admin app (reverse-proxy friendly)
    if (string.IsNullOrWhiteSpace(apiBase))
    {
        // In WASM, we can use the current location origin
        apiBase = builder.HostEnvironment.BaseAddress?.TrimEnd('/');
    }

    // Final fallback for local dev scenarios
    var baseUrl = string.IsNullOrWhiteSpace(apiBase) ? "https://localhost:7169" : apiBase;

    return new HttpClient { BaseAddress = new Uri(baseUrl) };
});

await builder.Build().RunAsync().ConfigureAwait(false);