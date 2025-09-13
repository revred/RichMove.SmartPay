namespace RichMove.SmartPay.Api.Idempotency;

public sealed class IdempotencyOptions
{
    public int Hours { get; set; } = 24;
}