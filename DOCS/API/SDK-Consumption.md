# SDK Consumption — C# and TypeScript (Plan)

> **Status:** Generated SDKs are planned in WP6.1; code below is **illustrative only**.

## C# (NuGet) — `RichMove.SmartPay.Sdk`

### Instantiate
```csharp
var client = new SmartPayClient(
    baseUrl: cfg["SmartPay:BaseUrl"],
    credential: new ClientCredentials(cfg["SmartPay:ClientId"], cfg["SmartPay:ClientSecret"]),
    tenantId: "blue");
```

### Create a quote (idempotent)
```csharp
var quote = await client.Fx.CreateQuoteAsync(new FxQuoteRequest
{
    FromCurrency = "USD",
    ToCurrency   = "GBP",
    Amount       = 2500m
}, idempotencyKey: Guid.NewGuid().ToString("N"));
```

### Paging through results (cursor)
```csharp
var page = await client.Fx.ListQuotesAsync(limit: 25);
while (!string.IsNullOrEmpty(page.NextCursor))
{
    page = await client.Fx.ListQuotesAsync(limit: 25, cursor: page.NextCursor);
}
```

### Webhook verification (HMAC, conceptual)
```csharp
var ok = WebhookVerifier.Verify(headers, body, secret); // true/false
```

---

## TypeScript (npm) — `@richmove/smartpay`

### Instantiate
```ts
import { SmartPayClient } from "@richmove/smartpay";

const client = new SmartPayClient({
  baseUrl: process.env.SMARTPAY_BASE_URL!,
  apiKey: process.env.SMARTPAY_API_KEY!,
  tenant: "blue"
});
```

### Create a quote
```ts
const q = await client.fx.createQuote({
  fromCurrency: "USD",
  toCurrency:   "GBP",
  amount:       2500
}, { idempotencyKey: crypto.randomUUID().replace(/-/g, "") });
```

### Iterate with a cursor
```ts
for await (const item of client.fx.iterQuotes({ limit: 25 })) {
  console.log(item.id);
}
```

### Verify webhook (conceptual)
```ts
import { verifyWebhook } from "@richmove/smartpay/webhooks";

const ok = verifyWebhook(req.headers, rawBody, process.env.WEBHOOK_SECRET!);
```

---

## Best Practices
- Use **Client‑Credentials** for servers, **API keys** for simple server integrations.
- Always pass **Idempotency‑Key** for POST/PUT.
- Expect **RFC7807 ProblemDetails** for errors; inspect `type`, `title`, `detail`.