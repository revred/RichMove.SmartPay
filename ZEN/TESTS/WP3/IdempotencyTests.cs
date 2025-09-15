using RichMove.SmartPay.Api.Idempotency;
using RichMove.SmartPay.Core.Time;
using Xunit;

namespace SmartPay.Tests.WP3;

public class IdempotencyTests
{
    [Fact]
    public async Task Duplicate_Key_Is_Rejected_Within_TTL()
    {
        var clock = new SystemClock();
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryIdempotencyStore>();
        var store = new InMemoryIdempotencyStore(clock, logger);

        var tenantKey1 = "t1::k1";
        var tenantKey2 = "t1::k1"; // Same key
        var expiresAt = clock.UtcNow.AddMinutes(10);

        var ok1 = await store.TryPutAsync(tenantKey1, expiresAt);
        var ok2 = await store.TryPutAsync(tenantKey2, expiresAt);

        Assert.True(ok1);
        Assert.False(ok2);
    }
}