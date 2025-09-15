using RichMove.SmartPay.Core.Time;
using System.Security.Cryptography;
using System.Text;

namespace RichMove.SmartPay.Api.Security;

/// <summary>
/// Webhook signature verification using HMAC SHA-256 + timestamp window
/// Prevents replay attacks and ensures webhook authenticity
/// </summary>
public sealed partial class WebhookSignature
{
    private readonly IClock _clock;
    private readonly ILogger<WebhookSignature> _logger;
    private readonly TimeSpan _timestampTolerance;

    public WebhookSignature(IClock clock, ILogger<WebhookSignature> logger, TimeSpan? timestampTolerance = null)
    {
        _clock = clock;
        _logger = logger;
        _timestampTolerance = timestampTolerance ?? TimeSpan.FromMinutes(5); // 5-minute window
    }

    /// <summary>
    /// Generate webhook signature for outbound webhooks
    /// Format: t={timestamp},v1={signature}
    /// </summary>
    public string GenerateSignature(string payload, string secret)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(secret);

        var timestamp = _clock.UtcNowOffset.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{payload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var signature = Convert.ToHexString(hash).ToUpperInvariant();

        return $"t={timestamp},v1={signature}";
    }

    /// <summary>
    /// Verify webhook signature for inbound webhooks
    /// </summary>
    public WebhookVerificationResult VerifySignature(string payload, string signature, string secret)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(secret);

        try
        {
            var parsedSignature = ParseSignature(signature);
            if (parsedSignature == null)
            {
                return new WebhookVerificationResult(false, "Invalid signature format");
            }

            // Check timestamp window to prevent replay attacks
            var currentTimestamp = _clock.UtcNowOffset.ToUnixTimeSeconds();
            var timeDifference = Math.Abs(currentTimestamp - parsedSignature.Value.Timestamp);

            if (timeDifference > _timestampTolerance.TotalSeconds)
            {
                return new WebhookVerificationResult(false, $"Timestamp outside tolerance window: {timeDifference}s");
            }

            // Verify signature
            var expectedPayload = $"{parsedSignature.Value.Timestamp}.{payload}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(expectedPayload));
            var expectedSignature = Convert.ToHexString(expectedHash).ToUpperInvariant();

            var isValid = ConstantTimeEquals(expectedSignature, parsedSignature.Value.Signature);

            if (isValid)
            {
                Log.WebhookSignatureVerified(_logger, payload.Length, parsedSignature.Value.Timestamp);
            }
            else
            {
                Log.WebhookSignatureInvalid(_logger, payload.Length);
            }

            return new WebhookVerificationResult(isValid, isValid ? null : "Signature mismatch");
        }
        catch (Exception ex)
        {
            Log.WebhookVerificationError(_logger, ex);
            return new WebhookVerificationResult(false, $"Verification error: {ex.Message}");
        }
    }

    private static (long Timestamp, string Signature)? ParseSignature(string signature)
    {
        // Expected format: t=1234567890,v1=abc123...
        var parts = signature.Split(',');
        if (parts.Length != 2) return null;

        var timestampPart = parts[0];
        var signaturePart = parts[1];

        if (!timestampPart.StartsWith("t=", StringComparison.Ordinal) ||
            !signaturePart.StartsWith("v1=", StringComparison.Ordinal))
        {
            return null;
        }

        if (!long.TryParse(timestampPart[2..], out var timestamp))
        {
            return null;
        }

        var signatureValue = signaturePart[3..];
        if (string.IsNullOrWhiteSpace(signatureValue))
        {
            return null;
        }

        return (timestamp, signatureValue);
    }

    /// <summary>
    /// Constant-time string comparison to prevent timing attacks
    /// </summary>
    private static bool ConstantTimeEquals(string expected, string actual)
    {
        if (expected.Length != actual.Length)
            return false;

        var result = 0;
        for (int i = 0; i < expected.Length; i++)
        {
            result |= expected[i] ^ actual[i];
        }

        return result == 0;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 5201, Level = LogLevel.Information, Message = "Webhook signature verified successfully for payload length {PayloadLength}, timestamp {Timestamp}")]
        public static partial void WebhookSignatureVerified(ILogger logger, int payloadLength, long timestamp);

        [LoggerMessage(EventId = 5202, Level = LogLevel.Warning, Message = "Webhook signature verification failed for payload length {PayloadLength}")]
        public static partial void WebhookSignatureInvalid(ILogger logger, int payloadLength);

        [LoggerMessage(EventId = 5203, Level = LogLevel.Error, Message = "Webhook signature verification error")]
        public static partial void WebhookVerificationError(ILogger logger, Exception exception);
    }
}

/// <summary>
/// Result of webhook signature verification
/// </summary>
public sealed record WebhookVerificationResult(
    bool IsValid,
    string? ErrorMessage = null);