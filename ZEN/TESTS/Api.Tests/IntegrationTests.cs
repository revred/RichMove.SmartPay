using FluentAssertions;
using RichMove.SmartPay.Core.ForeignExchange;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RichMove.SmartPay.Api.Tests;

#pragma warning disable CA1707, CA2234, CA2007
public class IntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public IntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ServiceInfo_ReturnsExpectedData()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("RichMove.SmartPay");
        content.Should().Contain("Ram Revanur");
    }

    [Fact]
    public async Task FxQuote_ReturnsSentinelValues()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var request = new FxQuoteRequest
        {
            FromCurrency = "GBP",
            ToCurrency = "USD",
            Amount = 100m
        };

        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/fx/quote", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("1.5"); // Sentinel rate
        responseContent.Should().Contain("150"); // Converted amount
    }

    [Fact]
    public async Task ShopInfo_ReturnsTestData()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/shop/info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("test-shop-123");
        content.Should().Contain("RichMove Test Shop");
    }
}
#pragma warning restore CA1707, CA2234, CA2007