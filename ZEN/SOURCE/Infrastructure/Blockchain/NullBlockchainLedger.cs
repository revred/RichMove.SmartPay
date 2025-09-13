using System.Security.Cryptography;
using System.Text;
using RichMove.SmartPay.Core.Blockchain;

namespace RichMove.SmartPay.Infrastructure.Blockchain;

public sealed class NullBlockchainLedger : IBlockchainLedger
{
    public Task<LedgerReceipt> AppendAsync(string canonicalJson, CancellationToken ct = default)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        var checksum = Convert.ToHexString(hash);

        var receipt = new LedgerReceipt(
            TxnId: $"null-{Guid.NewGuid():N}",
            BlockId: null,
            TimestampUtc: DateTime.UtcNow,
            Status: LedgerStatus.Disabled,
            Checksum: checksum,
            Metadata: "Ledger disabled via feature flag");

        return Task.FromResult(receipt);
    }
}