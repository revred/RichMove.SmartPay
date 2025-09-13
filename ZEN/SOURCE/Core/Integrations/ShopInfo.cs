namespace RichMove.SmartPay.Core.Integrations;

/// <summary>
/// Shopify shop information.
/// </summary>
public sealed record ShopInfo
{
    /// <summary>
    /// Shop ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Shop name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Shop domain.
    /// </summary>
    public required string Domain { get; init; }

    /// <summary>
    /// Shop currency.
    /// </summary>
    public required string Currency { get; init; }

    /// <summary>
    /// Whether the shop is active.
    /// </summary>
    public bool IsActive { get; init; } = true;
}