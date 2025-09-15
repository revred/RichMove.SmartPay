#pragma warning disable CA1707, CA1307
using RichMove.SmartPay.Core.Blockchain;
using RichMove.SmartPay.Infrastructure.Blockchain;
using System.Text.Json;

namespace RichMove.SmartPay.Core.Tests.Blockchain;

public class LedgerTests
{
    [Fact]
    public async Task NullLedger_ReturnsDisabledReceipt_WithChecksum()
    {
        var ledger = new NullBlockchainLedger();
        var json = JsonSerializer.Serialize(new { type = "fx.executed", amount = 100, ccy = "GBP" });

        var receipt = await ledger.AppendAsync(json);

        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Checksum);
        Assert.Equal(LedgerStatus.Disabled, receipt.Status);
        Assert.StartsWith("null-", receipt.TxnId);
        Assert.Contains("disabled", receipt.Metadata);
    }

    [Fact]
    public async Task InMemoryLedger_ReturnsConfirmedReceipt_WithBlockId()
    {
        var ledger = new InMemoryBlockchainLedger();
        var json = JsonSerializer.Serialize(new { type = "payment.settled", amount = 50.25m, currency = "USD" });

        var receipt = await ledger.AppendAsync(json);

        Assert.NotNull(receipt);
        Assert.NotNull(receipt.Checksum);
        Assert.NotNull(receipt.BlockId);
        Assert.Equal(LedgerStatus.Confirmed, receipt.Status);
        Assert.Equal(receipt.TxnId, receipt.BlockId); // Same for in-memory impl
        Assert.Contains("memory", receipt.Metadata);
    }

    [Fact]
    public async Task Ledger_SameJson_ProducesSameChecksum()
    {
        var nullLedger = new NullBlockchainLedger();
        var memoryLedger = new InMemoryBlockchainLedger();
        var json = JsonSerializer.Serialize(new { type = "test", data = "consistent" });

        var nullReceipt = await nullLedger.AppendAsync(json);
        var memoryReceipt = await memoryLedger.AppendAsync(json);

        Assert.Equal(nullReceipt.Checksum, memoryReceipt.Checksum);
    }
}