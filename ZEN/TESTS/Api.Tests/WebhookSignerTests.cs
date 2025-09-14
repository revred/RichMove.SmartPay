using System.Text.Json;
using SmartPay.Infrastructure.Webhooks;
using Xunit;

namespace SmartPay.Tests.WP5;

public class WebhookSignerTests
{
    [Fact]
    public void Signature_Computes_Deterministically()
    {
        var secret = "test_secret";
        var ts = 1700000000;
        var body = "{\"hello\":\"world\"}";

        var sig1 = WebhookSigner.ComputeSignature(secret, ts, body);
        var sig2 = WebhookSigner.ComputeSignature(secret, ts, body);

        Assert.Equal(sig1, sig2);
        Assert.StartsWith($"t={ts}, v1=", sig1);
        Assert.Equal(2, sig1.Split(',').Length);
    }
}