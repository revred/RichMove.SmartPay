namespace RichMove.SmartPay.Infrastructure.Integrations;

/// <summary>
/// Shopify configuration options.
/// </summary>
public sealed record ShopifyOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Shopify";

    /// <summary>
    /// Shopify API key.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Shopify API secret key.
    /// </summary>
    public required string SecretKey { get; init; }

    /// <summary>
    /// Shop domain.
    /// </summary>
    public required string ShopDomain { get; init; }

    /// <summary>
    /// API version.
    /// </summary>
    public string ApiVersion { get; init; } = "2024-10";
}