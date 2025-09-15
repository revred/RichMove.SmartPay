using SmartPay.Core.Payments.Idempotency;

namespace SmartPay.Tests.WP3;

public class IdempotencyTests
{
    [Fact]
    public async Task Duplicate_Key_Is_Rejected_Within_TTL()
    {
        var store = new InMemoryIdempotencyStore();
        var ok1 = await store.TryAddAsync("t1", "k1", TimeSpan.FromMinutes(10));
        var ok2 = await store.TryAddAsync("t1", "k1", TimeSpan.FromMinutes(10));
        Assert.True(ok1);
        Assert.False(ok2);
    }
}