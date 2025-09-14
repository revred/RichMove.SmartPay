using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;

namespace RichMove.SmartPay.Api.Tests.WP7;

#pragma warning disable CA1707, CA2234, CA2007
public class AdminPolicyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AdminPolicyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MetricsEndpoint_DisabledByDefault_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ScalingStatusEndpoint_DisabledByDefault_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/scaling/status");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MetricsEndpoint_NoAuth_Returns401()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:Monitoring:Enabled", "true");
            builder.UseSetting("Features:Monitoring:Prometheus", "true");
        }).CreateClient();

        var response = await client.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ScalingStatusEndpoint_NoAuth_Returns401()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:Scaling:Enabled", "true");
            builder.UseSetting("Features:Scaling:ExposeStatusEndpoint", "true");
        }).CreateClient();

        var response = await client.GetAsync("/scaling/status");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MetricsEndpoint_ValidAdminToken_Returns200()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:Monitoring:Enabled", "true");
            builder.UseSetting("Features:Monitoring:Prometheus", "true");
            builder.UseSetting("Admin:ApiKey", "test-admin-key-123");
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Admin-Token", "test-admin-key-123");

        var response = await client.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("smartpay_info");
        content.Should().Contain("service=\"smartpay\"");
    }

    [Fact]
    public async Task ScalingStatusEndpoint_ValidAdminToken_Returns200()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:Scaling:Enabled", "true");
            builder.UseSetting("Features:Scaling:ExposeStatusEndpoint", "true");
            builder.UseSetting("Admin:ApiKey", "test-admin-key-123");
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Admin-Token", "test-admin-key-123");

        var response = await client.GetAsync("/scaling/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("smartpay");
        content.Should().Contain("healthy");
        content.Should().NotContain("tenant");  // No PII
        content.Should().NotContain("user");    // No PII
    }

    [Fact]
    public async Task MetricsEndpoint_InvalidAdminToken_Returns401()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:Monitoring:Enabled", "true");
            builder.UseSetting("Features:Monitoring:Prometheus", "true");
            builder.UseSetting("Admin:ApiKey", "correct-key");
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Admin-Token", "wrong-key");

        var response = await client.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ScalingStatusEndpoint_InvalidAdminToken_Returns401()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:Scaling:Enabled", "true");
            builder.UseSetting("Features:Scaling:ExposeStatusEndpoint", "true");
            builder.UseSetting("Admin:ApiKey", "correct-key");
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Admin-Token", "wrong-key");

        var response = await client.GetAsync("/scaling/status");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
#pragma warning restore CA1707, CA2234, CA2007