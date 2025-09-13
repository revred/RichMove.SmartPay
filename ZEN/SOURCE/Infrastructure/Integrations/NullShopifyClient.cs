using RichMove.SmartPay.Core.Integrations;

namespace RichMove.SmartPay.Infrastructure.Integrations;

/// <summary>
/// Null implementation of Shopify client for WP1 testing.
/// Returns test data without making live network calls.
/// </summary>
public sealed class NullShopifyClient : IShopifyClient
{
    /// <inheritdoc />
    public Task<ShopInfo> GetShopInfoAsync(CancellationToken cancellationToken = default)
    {
        var shopInfo = new ShopInfo
        {
            Id = "test-shop-123",
            Name = "RichMove Test Shop",
            Domain = "richmove-test.myshopify.com",
            Currency = "GBP",
            IsActive = true
        };

        return Task.FromResult(shopInfo);
    }

    /// <inheritdoc />
    public bool ValidateWebhookSignature(string payload, string signature)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(signature);

        // In WP1, always return true for testing
        return true;
    }
}