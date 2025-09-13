using System.ComponentModel.DataAnnotations;
using System.Text;
using RichMove.SmartPay.Core.Patterns;

namespace RichMove.SmartPay.Api.Validation;

public static class InputValidator
{
    /// <summary>
    /// Canonicalize and validate currency codes
    /// Trims whitespace, normalizes to uppercase, validates ISO-4217 format
    /// </summary>
    public static ValidationResult ValidateCurrencyCode(string? currency, string fieldName)
    {
        if (string.IsNullOrEmpty(currency))
        {
            return new ValidationResult($"{fieldName} is required", new[] { fieldName });
        }

        // Canonicalization: trim and normalize
        var canonicalized = currency.Trim().ToUpperInvariant();

        // Reject if original had leading/trailing whitespace (security measure)
        if (canonicalized.Length != currency.Length)
        {
            return new ValidationResult($"{fieldName} cannot have leading or trailing whitespace", new[] { fieldName });
        }

        // Length check
        if (canonicalized.Length != 3)
        {
            return new ValidationResult($"{fieldName} must be exactly 3 characters", new[] { fieldName });
        }

        // Pattern validation (ISO-4217: 3 uppercase letters)
        if (!RegexPatterns.Iso4217().IsMatch(canonicalized))
        {
            return new ValidationResult($"{fieldName} must contain only uppercase letters A-Z", new[] { fieldName });
        }

        // Unicode confusables guard (basic check)
        if (ContainsNonAsciiLetters(canonicalized))
        {
            return new ValidationResult($"{fieldName} must contain only ASCII letters A-Z", new[] { fieldName });
        }

        return ValidationResult.Success!;
    }

    /// <summary>
    /// Validate and canonicalize client/correlation IDs
    /// </summary>
    public static ValidationResult ValidateIdentifier(string? identifier, string fieldName, int maxLength = 64)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return new ValidationResult($"{fieldName} is required", new[] { fieldName });
        }

        var canonicalized = identifier.Trim();

        // Reject if original had leading/trailing whitespace
        if (canonicalized.Length != identifier.Length)
        {
            return new ValidationResult($"{fieldName} cannot have leading or trailing whitespace", new[] { fieldName });
        }

        if (canonicalized.Length > maxLength)
        {
            return new ValidationResult($"{fieldName} cannot exceed {maxLength} characters", new[] { fieldName });
        }

        // Basic confusables check
        if (ContainsConfusableCharacters(canonicalized))
        {
            return new ValidationResult($"{fieldName} contains potentially confusing characters", new[] { fieldName });
        }

        return ValidationResult.Success!;
    }

    private static bool ContainsNonAsciiLetters(string input)
    {
        return input.Any(c => c < 'A' || c > 'Z');
    }

    private static readonly char[] ConfusableChars = ['\u0430', '\u043e', '\u0440', '\u0440', '\u0435']; // Cyrillic a,o,p,p,e

    private static bool ContainsConfusableCharacters(string input)
    {
        // Basic homoglyph detection for common confusables
        return input.Any(c => ConfusableChars.Contains(c) || c > 127); // Non-ASCII check
    }
}