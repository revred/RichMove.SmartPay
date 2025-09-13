# Testing Strategy (Intrusive)

- **Unit**: xUnit + FluentAssertions; pure domain logic must be 100% covered.
- **Property-based**: FsCheck for invariants (state machines, idempotency, quote rules).
- **Contract**: Schemathesis + Dredd vs OpenAPI.
- **Integration**: Testcontainers (Postgres + provider mocks).
- **Mutation**: Stryker.NET with break threshold 95%.
- **Chaos/Faults**: Polly chaos in test profile; random 429/500 and network jitter.
- **Load**: k6; SLO 95p <200ms @100 RPS; zero duplicate processing under idempotent POST storms.
- **Security**: Roslyn analyzers + dependency audit; JWT/claims tests.
