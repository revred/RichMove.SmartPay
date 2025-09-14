using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartPay.Core.MultiTenancy;

namespace SmartPay.Infrastructure.MultiTenancy;

public sealed class HostTenantResolver(IConfiguration cfg) : ITenantResolver
{
    public Task<string> ResolveAsync<TContext>(TContext context)
    {
        var strategy = cfg.GetValue("WP4:MultiTenancy:Strategy", "Host")!;
        var headerName = cfg.GetValue("WP4:MultiTenancy:Header", "X-Tenant")!;

        if (context is not HttpContext httpContext)
            return Task.FromResult(TenantId.Default);

        if (string.Equals(strategy, "Header", StringComparison.OrdinalIgnoreCase))
        {
            if (httpContext.Request.Headers.TryGetValue(headerName, out var tenantHeader) &&
                !string.IsNullOrWhiteSpace(tenantHeader))
                return Task.FromResult(tenantHeader.ToString());
        }

        // Default: derive from subdomain (e.g., foo.api.example.com → foo)
        var host = httpContext.Request.Host.Host ?? string.Empty;
        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3) // subdomain.domain.tld
            return Task.FromResult(parts[0]);

        return Task.FromResult(TenantId.Default);
    }
}

public static class MultiTenancyRegistration
{
    public static IServiceCollection Add(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddScoped<ITenantResolver, HostTenantResolver>();
        return services;
    }
}