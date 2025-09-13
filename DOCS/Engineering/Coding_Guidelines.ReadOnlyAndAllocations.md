# Read‑only & Allocation Guidelines (C#)

> Objective: small, surgical changes that reduce allocations and clarify intent—without rewrites.

## Read‑only intent
- Prefer `record` for DTOs and domain value objects.
- Mark fields `readonly` when set in ctor only.
- Mark structs `readonly` when all fields are immutable.
- Seal classes not designed for inheritance.
- Prefer `IReadOnlyList<>`/`IReadOnlyDictionary<>` on outward-facing APIs.
- Use `in` parameters for large structs; avoid for reference types.

## Strings & comparisons
- Always specify `StringComparison` (`Ordinal` or `OrdinalIgnoreCase` for tokens/IDs).
- Avoid `ToLower()/ToUpper()`; use case-insensitive comparisons.
- Avoid unnecessary substrings; prefer indexing/slicing (`ReadOnlySpan<char>` where hot).
- Trim and validate inputs at boundaries; reject mixed/hidden whitespace.

## Collections
- Pre-size `List<>`/`Dictionary<>` when count is known.
- Use `ArrayPool<T>` for large transient buffers.
- Avoid `.ToList()` on hot paths; stream results when possible.

## Regex
- Use `[GeneratedRegex]` for static patterns (compile‑time generated).
- Keep patterns central (`Patterns` class) for reuse.

## Logging
- Use `LoggerMessage.Define` for high-frequency logs to avoid boxing and message-template parsing costs.
- Prefer structured logging (key/value) over string concatenation.

## Async
- Return `ValueTask` only when a sync completion is common; otherwise use `Task`.
- Avoid async on methods that never await (will generate state machine).

## JSON
- Prefer `System.Text.Json` with source-gen (future) or careful options.
- Reuse `JsonSerializerOptions` singletons.
- Use `Utf8JsonWriter` for hot serialization loops (future).

## Examples
```csharp
// String comparison
if (!header.Equals("application/json", StringComparison.OrdinalIgnoreCase)) { ... }

// Generated regex (see Patterns below)
if (!Patterns.Iso4217().IsMatch(code)) return false;

// LoggerMessage.Define
private static readonly Action<ILogger, string, Exception?> _fxQuoted =
    LoggerMessage.Define<string>(LogLevel.Information, new EventId(1001, "FxQuoted"),
    "FX quoted for {Pair}");

public static void FxQuoted(ILogger logger, string pair) => _fxQuoted(logger, pair, null);
```