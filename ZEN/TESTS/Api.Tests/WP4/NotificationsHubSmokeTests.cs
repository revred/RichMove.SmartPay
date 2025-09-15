using RichMove.SmartPay.Api.Tests;
using System.Net;

namespace SmartPay.Tests.WP4;

public class NotificationsHubSmokeTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public NotificationsHubSmokeTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task NegotiationEndpoint_Should_Exist()
    {
        var resp = await _client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", new StringContent(""));
        Assert.NotEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }
}