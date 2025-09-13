namespace RichMove.SmartPay.Core.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}