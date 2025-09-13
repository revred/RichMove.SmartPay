using System.Text.RegularExpressions;

namespace RichMove.SmartPay.Core.Patterns;

public static partial class RegexPatterns
{
    // ISO-4217 currency code
    [GeneratedRegex("^[A-Z]{3}$")]
    public static partial Regex Iso4217();

    // Correlation-ID (basic GUID w/o braces)
    [GeneratedRegex("^[0-9a-fA-F\\-]{36}$")]
    public static partial Regex Guid36();
}