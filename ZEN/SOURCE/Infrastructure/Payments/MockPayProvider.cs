using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SmartPay.Core.Payments;

namespace SmartPay.Infrastructure.Payments;

public sealed class MockPayProvider : IPaymentProvider
{
    private readonly string _secret;
    public string Name => "MockPay";

    public MockPayProvider(IConfiguration cfg)
    {
        _secret = cfg["WP3:MockPay:Secret"] ?? "change-me";
    }

    public Task<PaymentIntentResult> CreateIntentAsync(PaymentIntentRequest req, CancellationToken ct = default)
    {
        // Simulate provider intent creation
        var intentId = "mpi_" + Guid.NewGuid().ToString("N")[..12];
        var clientSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        return Task.FromResult(new PaymentIntentResult(Name, intentId, "requires_confirmation", clientSecret));
    }

    public bool VerifySignature(string payload, string? signature, string secret)
    {
        if (string.IsNullOrWhiteSpace(signature)) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return string.Equals(hex, signature, StringComparison.Ordinal);
    }

    public string SecretForDocs() => _secret;
}