using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace RichMove.SmartPay.Api.Tests;

#pragma warning disable CA1707, CA2234, CA2007
public sealed class BlockchainEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public BlockchainEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateWallet_WhenBlockchainDisabled_Returns501()
    {
        var client = _factory.CreateClient();
        var request = new { ChainId = Guid.NewGuid(), Address = "0x123", Custody = "EXTERNAL" };

        var response = await client.PostAsJsonAsync("/v1/chain/wallets", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task CreateOnchainIntent_WhenBlockchainDisabled_Returns501()
    {
        var client = _factory.CreateClient();
        var request = new { SourceAssetId = Guid.NewGuid(), TargetAssetId = Guid.NewGuid(), AmountSource = 100m };

        var response = await client.PostAsJsonAsync("/v1/chain/intents/onchain", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task IngestTx_WhenBlockchainDisabled_Returns501()
    {
        var client = _factory.CreateClient();
        var request = new { ChainId = Guid.NewGuid(), TxHash = "0xabc123" };

        var response = await client.PostAsJsonAsync("/v1/chain/tx/ingest", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
    }
}
#pragma warning restore CA1707, CA2234, CA2007