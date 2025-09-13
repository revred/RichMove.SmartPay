using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace RichMove.SmartPay.Api.Tests;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Test fixture must be public for xUnit dependency injection")]
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Blockchain:Enabled"] = "false"
            });
        });

        base.ConfigureWebHost(builder);
    }
}