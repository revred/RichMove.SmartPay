namespace RichMove.SmartPay.Api;

public sealed class FeatureFlags
{
    public bool BlockchainEnabled { get; init; }
    public bool QuotesCacheEnabled { get; init; }
    public bool RateLimitEnabled { get; init; } = true;
}