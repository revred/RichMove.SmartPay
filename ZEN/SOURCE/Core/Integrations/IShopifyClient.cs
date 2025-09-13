namespace RichMove.SmartPay.Core.Integrations;

/// <summary>
/// Shopify client interface for e-commerce integration.
/// WP1: Typed interface only, no live network calls.
/// </summary>
public interface IShopifyClient
{
    /// <summary>
    /// Gets shop information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Shop information.</returns>
    Task<ShopInfo> GetShopInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates webhook signature.
    /// </summary>
    /// <param name="payload">Webhook payload.</param>
    /// <param name="signature">Webhook signature.</param>
    /// <returns>True if signature is valid.</returns>
    bool ValidateWebhookSignature(string payload, string signature);
}