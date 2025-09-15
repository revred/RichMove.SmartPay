namespace SmartPay.Core.Payments;

public record PaymentIntentRequest(string Currency, decimal Amount, string? Reference, string TenantId);
public record PaymentIntentResult(string Provider, string IntentId, string Status, string? ClientSecret);

public interface IPaymentProvider
{
    string Name { get; }
    Task<PaymentIntentResult> CreateIntentAsync(PaymentIntentRequest req, CancellationToken ct = default);
    /// <summary>Verify provider webhook signature against shared secret.</summary>
    bool VerifySignature(string payload, string? signature, string secret);
}