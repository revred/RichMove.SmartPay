#pragma warning disable CA1707, CA2007, CA1515
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RichMove.SmartPay.Api.Tests.Helpers;
using RichMove.SmartPay.Infrastructure.Blockchain.Repositories;
using System.Net;
using System.Net.Http.Json;

namespace RichMove.SmartPay.Api.Tests;

public sealed class BlockchainEndpointsSchemaTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public BlockchainEndpointsSchemaTests(WebApplicationFactory<Program> baseFactory) => _baseFactory = baseFactory;

    private WebApplicationFactory<Program> Factory(bool supabaseEnabled, bool blockchainEnabled, bool useFakes)
        => _baseFactory.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((ctx, cfg) =>
            {
                var mem = new Dictionary<string, string?>
                {
                    ["Supabase:Enabled"] = supabaseEnabled ? "true" : "false",
                    ["Blockchain:Enabled"] = blockchainEnabled ? "true" : "false"
                };
                cfg.AddInMemoryCollection(mem);
            });

            if (useFakes)
            {
                b.ConfigureServices(svc =>
                {
                    svc.AddSingleton<IWalletRepository, FakeWalletRepository>();
                    svc.AddSingleton<IIntentRepository, FakeIntentRepository>();
                    svc.AddSingleton<ITxRepository, FakeTxRepository>();
                });
            }
        });

    [Fact]
    public async Task Endpoints_Return_404_When_Disabled()
    {
        var client = Factory(supabaseEnabled: true, blockchainEnabled: false, useFakes: false).CreateClient();

        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var resp1 = await client.PostAsJsonAsync("/v1/chain/wallets", new { chainId = Guid.NewGuid(), address = "0xabc" });

        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var resp2 = await client.PostAsJsonAsync("/v1/chain/intents/onchain", new { sourceAssetId = Guid.NewGuid(), targetAssetId = Guid.NewGuid(), amountSource = 1.23m });

        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var resp3 = await client.PostAsJsonAsync("/v1/chain/tx/ingest", new { chainId = Guid.NewGuid(), txHash = "0xdeadbeef" });

        Assert.Equal(HttpStatusCode.NotFound, resp1.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, resp2.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, resp3.StatusCode);

        Assert.Equal("blockchain", resp1.Headers.GetValues("X-Feature-Disabled").First());
        Assert.Equal("blockchain", resp2.Headers.GetValues("X-Feature-Disabled").First());
        Assert.Equal("blockchain", resp3.Headers.GetValues("X-Feature-Disabled").First());
    }

    [Fact]
    public async Task CreateWallet_Response_Matches_Schema_When_Enabled_With_Fakes()
    {
        var client = Factory(supabaseEnabled: true, blockchainEnabled: true, useFakes: true).CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var payload = new { chainId = Guid.NewGuid(), address = "0xabc", custody = "EXTERNAL" };
        var resp = await client.PostAsJsonAsync("/v1/chain/wallets", payload);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        await JsonSchemaAssert.ValidatesAgainstAsync("Schemas/chain_wallet_create.schema.json", json);
    }

    [Fact]
    public async Task CreateIntent_Response_Matches_Schema_When_Enabled_With_Fakes()
    {
        var client = Factory(supabaseEnabled: true, blockchainEnabled: true, useFakes: true).CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var payload = new { sourceAssetId = Guid.NewGuid(), targetAssetId = Guid.NewGuid(), amountSource = 10.50m };
        var resp = await client.PostAsJsonAsync("/v1/chain/intents/onchain", payload);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        await JsonSchemaAssert.ValidatesAgainstAsync("Schemas/chain_intent_create.schema.json", json);
    }

    [Fact]
    public async Task IngestTx_Response_Matches_Schema_When_Enabled_With_Fakes()
    {
        var client = Factory(supabaseEnabled: true, blockchainEnabled: true, useFakes: true).CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var payload = new { chainId = Guid.NewGuid(), txHash = "0xdeadbeefcafebabe", assetId = Guid.NewGuid(), amount = 1.0m };
        var resp = await client.PostAsJsonAsync("/v1/chain/tx/ingest", payload);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        await JsonSchemaAssert.ValidatesAgainstAsync("Schemas/chain_tx_ingest.schema.json", json);
    }

    [Fact]
    public async Task Validation_Fails_On_Bad_Request_Shapes()
    {
        var client = Factory(supabaseEnabled: true, blockchainEnabled: true, useFakes: true).CreateClient();

        // Missing address
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var badWallet = await client.PostAsJsonAsync("/v1/chain/wallets", new { chainId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, badWallet.StatusCode);

        // Negative amount
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var badIntent = await client.PostAsJsonAsync("/v1/chain/intents/onchain", new { sourceAssetId = Guid.NewGuid(), targetAssetId = Guid.NewGuid(), amountSource = -1 });
        Assert.Equal(HttpStatusCode.BadRequest, badIntent.StatusCode);

        // Missing tx hash
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var badTx = await client.PostAsJsonAsync("/v1/chain/tx/ingest", new { chainId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, badTx.StatusCode);
    }
}