namespace RichMove.SmartPay.Api.Diagnostics;

public static class Log
{
    // Pre-allocated log scopes for frequent fields (zero allocation after first call)
    private static readonly Func<ILogger, string, IDisposable?> _correlationIdScope =
        LoggerMessage.DefineScope<string>("CorrelationId: {CorrelationId}");

    private static readonly Func<ILogger, string, IDisposable?> _clientIdScope =
        LoggerMessage.DefineScope<string>("ClientId: {ClientId}");

    private static readonly Func<ILogger, string, string, IDisposable?> _operationScope =
        LoggerMessage.DefineScope<string, string>("Component: {Component}, Operation: {Operation}");

    private static readonly Func<ILogger, string, string, IDisposable?> _currencyScope =
        LoggerMessage.DefineScope<string, string>("CurrencyPair: {FromCurrency}->{ToCurrency}");

    // High-performance scope creation (zero allocation)
    public static IDisposable? CorrelationIdScope(ILogger logger, string correlationId) =>
        _correlationIdScope(logger, correlationId);

    public static IDisposable? ClientIdScope(ILogger logger, string clientId) =>
        _clientIdScope(logger, clientId);

    public static IDisposable? OperationScope(ILogger logger, string component, string operation) =>
        _operationScope(logger, component, operation);

    public static IDisposable? CurrencyScope(ILogger logger, string fromCurrency, string toCurrency) =>
        _currencyScope(logger, fromCurrency, toCurrency);

    // High-performance logging methods
    private static readonly Action<ILogger, string, string, Exception?> _fxQuoted =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(1001, nameof(FxQuoted)),
            "FX quoted for {Base}->{Quote}");

    private static readonly Action<ILogger, string, Exception?> _idempotencyConflict =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2002, nameof(IdempotencyConflict)),
            "Idempotency conflict for key {Key}");

    private static readonly Action<ILogger, string, long, Exception?> _healthCheckCompleted =
        LoggerMessage.Define<string, long>(LogLevel.Information, new EventId(2003, nameof(HealthCheckCompleted)),
            "Health check completed: {Status}, duration: {DurationMs}ms");

    private static readonly Action<ILogger, long, Exception?> _coldStartCompleted =
        LoggerMessage.Define<long>(LogLevel.Information, new EventId(4901, nameof(ColdStartCompleted)),
            "Cold-start: first request processed, latency: {LatencyMs}ms");

    public static void FxQuoted(ILogger logger, string @base, string quote)
        => _fxQuoted(logger, @base, quote, null);

    public static void IdempotencyConflict(ILogger logger, string key)
        => _idempotencyConflict(logger, key, null);

    public static void HealthCheckCompleted(ILogger logger, string status, long durationMs)
        => _healthCheckCompleted(logger, status, durationMs, null);

    public static void ColdStartCompleted(ILogger logger, long latencyMs)
        => _coldStartCompleted(logger, latencyMs, null);
}