namespace RichMove.SmartPay.Infrastructure.Data;

/// <summary>
/// Supabase configuration options.
/// </summary>
public sealed record SupabaseOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Supabase";

    /// <summary>
    /// Supabase project URL.
    /// </summary>
    public required Uri Url { get; init; }

    /// <summary>
    /// Supabase anon key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Whether Supabase is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;
}