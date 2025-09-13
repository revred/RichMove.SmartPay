namespace RichMove.SmartPay.Core.Blockchain;

public enum LedgerStatus { Disabled, Accepted, Confirmed, Failed }

public sealed record LedgerReceipt(
    string TxnId,
    string? BlockId,
    DateTime TimestampUtc,
    LedgerStatus Status,
    string Checksum,
    string? Metadata);

public interface IBlockchainLedger
{
    /// <summary>Append a canonical JSON payload to the ledger.</summary>
    Task<LedgerReceipt> AppendAsync(string canonicalJson, CancellationToken ct = default);
}