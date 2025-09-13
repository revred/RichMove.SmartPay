namespace RichMove.SmartPay.Core.Time;

/// <summary>
/// Production clock implementation using system time
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow;
}