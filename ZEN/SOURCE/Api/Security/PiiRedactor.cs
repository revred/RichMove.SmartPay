using System.Text.RegularExpressions;

namespace RichMove.SmartPay.Api.Security;

/// <summary>
/// PII redaction helper - one place to scrub logs
/// Centralized sensitive data redaction for logging safety
/// </summary>
public static partial class PiiRedactor
{
    private const string Redacted = "***REDACTED***";
    private const string PartialRedacted = "***PARTIAL***";

    // Credit card patterns (basic PAN detection)
    [GeneratedRegex(@"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex CreditCardPattern();

    // Email patterns
    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex EmailPattern();

    // Phone number patterns (basic international)
    [GeneratedRegex(@"\b(\+\d{1,3}[-.\s]?)?\(?\d{1,4}\)?[-.\s]?\d{1,4}[-.\s]?\d{1,9}\b", RegexOptions.Compiled)]
    private static partial Regex PhonePattern();

    // API key patterns (common formats)
    [GeneratedRegex(@"\b[A-Za-z0-9]{32,}\b", RegexOptions.Compiled)]
    private static partial Regex ApiKeyPattern();

    /// <summary>
    /// Redact PII from log messages
    /// </summary>
    public static string RedactForLogging(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input ?? string.Empty;

        var redacted = input;

        // Redact credit cards (keep last 4 for debugging)
        redacted = CreditCardPattern().Replace(redacted, match =>
        {
            var card = match.Value.Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
            return card.Length >= 4 ? $"****-****-****-{card[^4..]}" : Redacted;
        });

        // Redact emails (keep domain for debugging)
        redacted = EmailPattern().Replace(redacted, match =>
        {
            var email = match.Value;
            var atIndex = email.IndexOf('@', StringComparison.Ordinal);
            return atIndex > 0 ? $"{PartialRedacted}@{email[(atIndex + 1)..]}" : Redacted;
        });

        // Redact phone numbers completely
        redacted = PhonePattern().Replace(redacted, Redacted);

        // Redact potential API keys (long alphanumeric strings)
        redacted = ApiKeyPattern().Replace(redacted, match =>
        {
            var key = match.Value;
            return key.Length > 8 ? $"{key[..4]}...{key[^4..]}" : Redacted;
        });

        return redacted;
    }

    /// <summary>
    /// Redact currency amounts (keep currency code for debugging)
    /// </summary>
    public static string RedactCurrencyAmount(decimal amount, string currency)
    {
        return $"*****.** {currency}";
    }

    /// <summary>
    /// Redact wallet addresses (keep first/last 4 chars for debugging)
    /// </summary>
    public static string RedactWalletAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || address.Length <= 8)
            return Redacted;

        return $"{address[..4]}...{address[^4..]}";
    }

    /// <summary>
    /// Redact correlation IDs (keep first 8 chars for debugging)
    /// </summary>
    public static string RedactCorrelationId(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length <= 8)
            return PartialRedacted;

        return $"{correlationId[..8]}...";
    }
}