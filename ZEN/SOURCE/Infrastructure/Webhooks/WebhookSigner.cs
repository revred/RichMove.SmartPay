using System.Security.Cryptography;
using System.Text;

namespace SmartPay.Infrastructure.Webhooks;

public static class WebhookSigner
{
    /// <summary>
    /// Computes an HMAC SHA-256 signature over "t.{body}" using the shared secret.
    /// </summary>
    public static string ComputeSignature(string secret, long timestampUnix, string body)
    {
        var signedPayload = $"t={timestampUnix}.{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return $"t={timestampUnix}, v1={hex}";
    }
}