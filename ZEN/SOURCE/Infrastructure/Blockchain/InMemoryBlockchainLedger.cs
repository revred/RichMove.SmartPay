using RichMove.SmartPay.Core.Blockchain;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace RichMove.SmartPay.Infrastructure.Blockchain;

public sealed class InMemoryBlockchainLedger : IBlockchainLedger
{
    private readonly ConcurrentQueue<string> _chain = new();

    public Task<LedgerReceipt> AppendAsync(string canonicalJson, CancellationToken ct = default)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        var checksum = Convert.ToHexString(hash);
        var blockId = Guid.NewGuid().ToString("N");

        _chain.Enqueue($"{blockId}:{checksum}:{canonicalJson}");

        var receipt = new LedgerReceipt(
            TxnId: blockId,
            BlockId: blockId,
            TimestampUtc: DateTime.UtcNow,
            Status: LedgerStatus.Confirmed,
            Checksum: checksum,
            Metadata: "In‑memory block appended (dev only)");

        return Task.FromResult(receipt);
    }
}