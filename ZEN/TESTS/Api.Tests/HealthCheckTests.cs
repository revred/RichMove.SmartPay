using FluentAssertions;
using System.Net;

namespace RichMove.SmartPay.Api.Tests;

#pragma warning disable CA1707, CA2234, CA2007
public class HealthCheckTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public HealthCheckTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthCheck_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
}
#pragma warning restore CA1707, CA2234, CA2007