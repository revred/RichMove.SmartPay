namespace SmartPay.Core.MultiTenancy;

public interface ITenantResolver
{
    Task<string> ResolveAsync<TContext>(TContext context);
}

public static class TenantId
{
    public const string Default = "default";
}