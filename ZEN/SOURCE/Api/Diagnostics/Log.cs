using System;
using Microsoft.Extensions.Logging;

namespace RichMove.SmartPay.Api.Diagnostics;

public static class Log
{
    private static readonly Action<ILogger, string, string, Exception?> _fxQuoted =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(1001, nameof(FxQuoted)),
            "FX quoted for {Base}->{Quote}");

    private static readonly Action<ILogger, string, Exception?> _idempotencyConflict =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2001, nameof(IdempotencyConflict)),
            "Idempotency conflict for key {Key}");

    public static void FxQuoted(ILogger logger, string @base, string quote)
        => _fxQuoted(logger, @base, quote, null);

    public static void IdempotencyConflict(ILogger logger, string key)
        => _idempotencyConflict(logger, key, null);
}