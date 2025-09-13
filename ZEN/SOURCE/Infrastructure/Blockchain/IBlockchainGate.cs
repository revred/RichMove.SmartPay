using Microsoft.Extensions.Configuration;

namespace RichMove.SmartPay.Infrastructure.Blockchain;

/// <summary>
/// Feature gate for minimal blockchain endpoints. Keep the complexity isolated.
/// </summary>
public interface IBlockchainGate
{
    bool Enabled { get; }
}

public sealed class BlockchainGate : IBlockchainGate
{
    public BlockchainGate(IConfiguration cfg)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        Enabled = string.Equals(cfg["Blockchain:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
    }

    public bool Enabled { get; }
}