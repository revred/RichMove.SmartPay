# Zero‑ish Allocation Playbook (when it matters)

> Use only on hot paths. Profile first; optimize second.

## Strings
- Avoid `string.Concat`/interpolation in tight loops; prefer structured logging.
- Avoid case transforms; use `StringComparison` overloads.
- Avoid `Substring`; use ranges or spans where beneficial.

## Buffers
- Use `ArrayPool<T>` for transient buffers > 4 KB.
- Always return pooled arrays in `finally`.

## JSON
- Reuse `JsonSerializerOptions` instances.
- Consider `Utf8JsonWriter` for hot serialization; keep payloads small and flat.

## Collections
- Pre-size collections when count is known.
- Prefer `foreach` over LINQ in tight loops.

## Exceptions
- Do not use exceptions for control flow; return `Result<T>`/error code objects at boundaries.

## Logging
- Use `LoggerMessage.Define` for high-frequency paths.

## Tooling
- BenchmarkDotNet projects (future) to measure changes; never optimize blind.