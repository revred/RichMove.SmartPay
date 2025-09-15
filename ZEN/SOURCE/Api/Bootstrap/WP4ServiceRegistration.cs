namespace SmartPay.Api.Bootstrap;

public sealed class Wp4Options
{
    public bool NotificationsEnabled { get; init; }
    public string NotificationsProvider { get; init; } = "InMemory";

    public bool MultiTenancyEnabled { get; init; }
    public string MultiTenancyStrategy { get; init; } = "Host"; // or "Header"
    public string TenantHeader { get; init; } = "X-Tenant";

    public bool AnalyticsEnabled { get; init; }
    public bool RequestLogging { get; init; }
    public bool TriggersFxQuoteCreated { get; init; }

    public Wp4Options(IConfiguration cfg)
    {
        NotificationsEnabled = cfg.GetValue("WP4:Notifications:Enabled", true);
        NotificationsProvider = cfg.GetValue("WP4:Notifications:Provider", "InMemory")!;

        MultiTenancyEnabled = cfg.GetValue("WP4:MultiTenancy:Enabled", true);
        MultiTenancyStrategy = cfg.GetValue("WP4:MultiTenancy:Strategy", "Host")!;
        TenantHeader = cfg.GetValue("WP4:MultiTenancy:Header", "X-Tenant")!;

        AnalyticsEnabled = cfg.GetValue("WP4:Analytics:Enabled", true);
        RequestLogging = cfg.GetValue("WP4:Analytics:RequestLogging", true);
        TriggersFxQuoteCreated = cfg.GetValue("WP4:Triggers:FxQuoteCreated", true);
    }
}