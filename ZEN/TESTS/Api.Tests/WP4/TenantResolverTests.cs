using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SmartPay.Infrastructure.MultiTenancy;

namespace SmartPay.Tests.WP4;

public class TenantResolverTests
{
    private static IConfiguration HeaderStrategyConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WP4:MultiTenancy:Strategy"] = "Header",
            ["WP4:MultiTenancy:Header"] = "X-Tenant"
        }).Build();

    private static IConfiguration HostStrategyConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WP4:MultiTenancy:Strategy"] = "Host"
        }).Build();

    [Fact]
    public async Task HeaderStrategy_Uses_Header_Value()
    {
        var resolver = new HostTenantResolver(HeaderStrategyConfig());
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Tenant"] = "blue";
        var id = await resolver.ResolveAsync(ctx);
        Assert.Equal("blue", id);
    }

    [Fact]
    public async Task HostStrategy_Parses_Subdomain()
    {
        var resolver = new HostTenantResolver(HostStrategyConfig());
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString("alpha.api.example.com");
        var id = await resolver.ResolveAsync(ctx);
        Assert.Equal("alpha", id);
    }

    [Fact]
    public async Task HostStrategy_Defaults_When_No_Subdomain()
    {
        var resolver = new HostTenantResolver(HostStrategyConfig());
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString("example.com");
        var id = await resolver.ResolveAsync(ctx);
        Assert.Equal("default", id);
    }
}