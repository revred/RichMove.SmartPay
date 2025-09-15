namespace RichMove.SmartPay.Api.Idempotency;

public interface IIdempotencyStore
{
    Task<bool> TryPutAsync(string key, DateTime expiresUtc, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}