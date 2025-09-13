using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;

namespace RichMove.SmartPay.Api.Tests;

#pragma warning disable CA1707, CA2234, CA2007
public sealed class FxQuoteFastEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public FxQuoteFastEndpointsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Post_Quotes_Returns_200_With_Valid_Input()
    {
        var client = _factory.CreateClient();
        var req = new { fromCurrency = "GBP", toCurrency = "USD", amount = 100m };

        var resp = await client.PostAsJsonAsync("/v1/fx/quotes", req);
        resp.EnsureSuccessStatusCode();

        var content = await resp.Content.ReadAsStringAsync();
        content.Should().Contain("1.5"); // Sentinel rate
        content.Should().Contain("150"); // Converted amount
    }

    [Fact]
    public async Task Post_Quotes_Returns_400_On_Bad_Input()
    {
        var client = _factory.CreateClient();
        var bad = new { fromCurrency = "GB", toCurrency = "", amount = 0m };

        var resp = await client.PostAsJsonAsync("/v1/fx/quotes", bad);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
#pragma warning restore CA1707, CA2234, CA2007