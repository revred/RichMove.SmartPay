using SmartPay.Infrastructure.Webhooks;
using Xunit;

namespace SmartPay.Tests.WP5;

public class WebhookSignerTests
{
    [Fact]
    public void Creates_HMAC_Signature()
    {
        var signer = new InMemoryOutbox();
        var sig = signer.Sign("payload", "secret");
        Assert.NotNull(sig);
        Assert.Equal(64, sig.Length);
    }
}