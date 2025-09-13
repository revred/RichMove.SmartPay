using FluentAssertions;
using RichMove.SmartPay.Core.ForeignExchange;
using RichMove.SmartPay.Infrastructure.ForeignExchange;

namespace RichMove.SmartPay.Core.Tests.ForeignExchange;

#pragma warning disable CA1707
public class NullFxQuoteProviderTests
{
    [Fact]
    public async Task GetQuoteAsync_ValidRequest_ReturnsSentinelResult()
    {
        // Arrange
        var provider = new NullFxQuoteProvider();
        var request = new FxQuoteRequest
        {
            FromCurrency = "GBP",
            ToCurrency = "USD",
            Amount = 100m
        };

        // Act
        var result = await provider.GetQuoteAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Rate.Should().Be(1.5m); // Sentinel rate
        result.ConvertedAmount.Should().Be(150m); // 100 * 1.5
        result.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetQuoteAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new NullFxQuoteProvider();

        // Act & Assert
        await provider.Invoking(p => p.GetQuoteAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }
}
#pragma warning restore CA1707