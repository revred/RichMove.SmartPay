namespace SmartPay.Core.MultiTenancy;

public sealed record TenantContext(string TenantId)
{
    private static readonly AsyncLocal<TenantContext> _current = new();
    public static TenantContext Current
    {
        get => _current.Value ?? Empty;
        set => _current.Value = value;
    }

    public static TenantContext Empty { get; } = new(SmartPay.Core.MultiTenancy.TenantId.Default);
}