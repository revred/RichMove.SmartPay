# OpenTelemetry Wiring Plan - SmartPay Platform

## Namespace Convention
All telemetry uses namespace: `richmove.smartpay.*`

## 1. Traces (Distributed Tracing)

### Service Names
- `richmove.smartpay.api` - Main API service
- `richmove.smartpay.blockchain` - Blockchain operations
- `richmove.smartpay.fx` - Foreign exchange operations

### Trace Operations
```csharp
// Activity names follow pattern: {service}.{operation}
public static class ActivityNames
{
    public const string FxQuote = "richmove.smartpay.fx.quote";
    public const string BlockchainWallet = "richmove.smartpay.blockchain.wallet";
    public const string BlockchainTx = "richmove.smartpay.blockchain.transaction";
    public const string IdempotencyCheck = "richmove.smartpay.api.idempotency";
    public const string HealthCheck = "richmove.smartpay.api.health";
}
```

### Trace Attributes (Semantic Conventions)
```csharp
// Currency operations
["richmove.smartpay.currency.from"] = "USD"
["richmove.smartpay.currency.to"] = "GBP"
["richmove.smartpay.amount"] = "100.00"

// Blockchain operations
["richmove.smartpay.wallet.address"] = "0x1234...abcd"
["richmove.smartpay.blockchain.network"] = "ethereum"
["richmove.smartpay.tx.hash"] = "0xabcd...1234"

// Request context
["richmove.smartpay.correlation_id"] = correlation_id
["richmove.smartpay.idempotency_key"] = idempotency_key_partial
```

## 2. Metrics (Performance Monitoring)

### Counter Metrics
```csharp
// Request volume
"richmove.smartpay.requests.total"
    Tags: ["endpoint", "method", "status_code"]

// Business metrics
"richmove.smartpay.fx.quotes.total"
    Tags: ["currency_pair", "status"]

"richmove.smartpay.blockchain.transactions.total"
    Tags: ["network", "status", "operation"]

// Error tracking
"richmove.smartpay.errors.total"
    Tags: ["error_type", "component", "severity"]
```

### Histogram Metrics
```csharp
// Request duration
"richmove.smartpay.request.duration"
    Unit: "ms", Tags: ["endpoint", "method"]

// Business operation latency
"richmove.smartpay.fx.quote.duration"
    Unit: "ms", Tags: ["provider", "currency_pair"]

"richmove.smartpay.blockchain.operation.duration"
    Unit: "ms", Tags: ["operation", "network"]
```

### Gauge Metrics
```csharp
// System health
"richmove.smartpay.health.status"
    Tags: ["component", "check_type"]

// Resource usage
"richmove.smartpay.idempotency.keys.active"
"richmove.smartpay.connections.active"
    Tags: ["pool_name"]
```

## 3. Logs (Structured Logging)

### Log Event IDs
```csharp
// 1000-1999: General operations
1001: FxQuoted
1002: CorrelationIdGenerated

// 2000-2999: Infrastructure
2001: ColdStartCleanup
2002: IdempotencyConflict
2003: HealthCheckCompleted

// 3000-3999: Security
3001: EnvironmentVariableAccessed
3002: PiiRedacted
3003: WebhookSignatureVerified

// 4000-4999: Blockchain
4001: WalletCreated
4002: TransactionIngested
4003: BlockchainLedgerBound

// 9000+: Errors and warnings
9001: UnhandledException
9002: ValidationFailed
```

### Structured Log Fields
```csharp
// Always present
"@timestamp", "level", "message", "eventId"

// Request context (when available)
"correlationId", "requestId", "userId", "clientId"

// Operation context
"component", "operation", "duration_ms"

// Business context
"currency", "amount", "walletAddress", "txHash"
```

## Implementation Phases

### Phase 1: Foundation (Immediate)
- [ ] Add OpenTelemetry packages
- [ ] Configure basic tracing for HTTP requests
- [ ] Set up structured logging with EventIds
- [ ] Implement correlation ID propagation

### Phase 2: Business Metrics (Week 1)
- [ ] Add FX quote tracing and metrics
- [ ] Add blockchain operation tracking
- [ ] Implement health check instrumentation
- [ ] Add error rate monitoring

### Phase 3: Advanced Observability (Week 2)
- [ ] Custom span processors for business events
- [ ] Performance counters and resource metrics
- [ ] Rate limiting counters and dashboards
- [ ] Cold-start tracking implementation

### Phase 4: Production Readiness (Week 3)
- [ ] Sampling configuration for high volume
- [ ] Export to production backends (Jaeger, Prometheus)
- [ ] Dashboard templates and alerting rules
- [ ] Runbook integration with telemetry

## Configuration Example

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing =>
        tracing
            .SetServiceName("richmove.smartpay.api")
            .SetServiceVersion("1.0.0")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("richmove.smartpay.*")
            .AddJaegerExporter())
    .WithMetrics(metrics =>
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("richmove.smartpay.*")
            .AddPrometheusExporter());
```