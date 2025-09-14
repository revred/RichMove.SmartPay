using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SmartPay.Core.MultiTenancy;

namespace SmartPay.Api.Middleware;

public sealed class TenantMiddleware(RequestDelegate next, IConfiguration cfg, ITenantResolver resolver)
{
    public async Task Invoke(HttpContext context)
    {
        var tenantId = await resolver.ResolveAsync(context);
        TenantContext.Current = new TenantContext(tenantId);
        try
        {
            await next(context);
        }
        finally
        {
            TenantContext.Current = TenantContext.Empty;
        }
    }
}