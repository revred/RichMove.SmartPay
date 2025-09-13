namespace RichMove.SmartPay.Core.Time;

/// <summary>
/// Clock abstraction for testability and consistency
/// Wall-clock dependency isolation
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
    DateTimeOffset UtcNowOffset { get; }
}