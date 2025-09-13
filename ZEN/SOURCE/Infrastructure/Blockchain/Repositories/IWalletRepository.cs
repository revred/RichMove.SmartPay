namespace RichMove.SmartPay.Infrastructure.Blockchain.Repositories;

public interface IWalletRepository
{
    Task<Guid> CreateAsync(Guid chainId, string address, Guid? userId, string custody = "EXTERNAL",
        string[]? tags = null, IDictionary<string, object>? metadata = null, CancellationToken ct = default);
}