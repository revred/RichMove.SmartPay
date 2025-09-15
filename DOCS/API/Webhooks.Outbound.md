# Outbound Webhooks (WP5)

## Delivery
- `POST` JSON with header `X-SmartPay-Signature: <hex hmacsha256(payload, secret)>`.
- Retries: exponential backoff; dead-letter after max attempts.

## Consumer example (C#)
```csharp
app.MapPost("/merchant/webhooks/smartpay", async (HttpRequest req) =>
{
    using var r = new StreamReader(req.Body);
    var payload = await r.ReadToEndAsync();
    var sig = req.Headers["X-SmartPay-Signature"].ToString();
    var secret = Environment.GetEnvironmentVariable("SMARTPAY_WEBHOOK_SECRET")!;
    var ok = Crypto.VerifyHmacSha256Hex(payload, sig, secret);
    return ok ? Results.Ok() : Results.BadRequest();
});
```

## Events
- `payment.intent.created`
- `payment.intent.succeeded`

## V&V
| Feature | Test | Evidence |
|---|---|---|
| Signed delivery | SMK-WP5-Signature | Receiver logs |
| Retry/backoff | OBS-WP5-Backoff | Worker logs show increasing delay |