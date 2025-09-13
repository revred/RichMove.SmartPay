# RichMove.SmartPay Coding Guidelines

**Project conceived and specified by Ram Revanur**

This document defines the coding standards, practices, and guidelines for the RichMove.SmartPay payment orchestration platform.

## Table of Contents

1. [Code Organization](#code-organization)
2. [C# Coding Standards](#c-coding-standards)
3. [Testing Guidelines](#testing-guidelines)
4. [Security Requirements](#security-requirements)
5. [Performance Standards](#performance-standards)
6. [Documentation Requirements](#documentation-requirements)
7. [Git & Branch Strategy](#git--branch-strategy)
8. [Quality Gates](#quality-gates)
9. [Architecture Patterns](#architecture-patterns)

---

## Code Organization

### Project Structure

```
ZEN/
├── SOURCE/                          # All source code
│   ├── Core/                       # Domain models, business logic
│   ├── Infrastructure/             # External integrations, persistence
│   └── Api/                        # Web API controllers, minimal APIs
├── TESTS/                          # All test projects
│   ├── Core.Tests/                 # Unit & property-based tests
│   └── Api.Tests/                  # Integration & contract tests
└── Build files (*.sln, *.props)    # Solution & configuration
```

### Naming Conventions

- **Projects**: `RichMove.SmartPay.[Layer]`
- **Namespaces**: Match folder structure
- **Classes**: PascalCase (`PaymentProcessor`)
- **Methods**: PascalCase (`ProcessPayment`)
- **Fields**: camelCase with underscore prefix (`_paymentService`)
- **Properties**: PascalCase (`PaymentId`)
- **Constants**: PascalCase (`MaxRetryAttempts`)
- **Test Methods**: `MethodName_Scenario_ExpectedResult`

---

## Language Policy

### Approved Languages
- **C# (.NET 9.0)** - Primary development language for all application code
- **Git Bash Scripts** - Automation and tooling scripts placed in `ZEN/TOOLS/`

### Prohibited Languages
- Python scripts (except existing CI/CD pipeline components)
- JavaScript/TypeScript for backend development
- PowerShell scripts
- Other scripting languages

**Rationale:**
- Maintain consistency across the codebase
- Leverage .NET ecosystem and tooling
- Ensure all team members can maintain and extend the codebase
- Simplify CI/CD and deployment processes

### Script Placement
- All automation scripts must be placed in `ZEN/TOOLS/`
- Scripts must have `.sh` extension for git bash compatibility
- Scripts must include proper shebang: `#!/bin/bash`

---

## C# Coding Standards

### Language Features

```csharp
// ✅ Use C# 13 features (.NET 9)
public readonly record struct PaymentId(Guid Value);

// ✅ File-scoped namespaces
namespace RichMove.SmartPay.Core.Payments;

// ✅ Nullable reference types enabled
public class PaymentService
{
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(ILogger<PaymentService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}

// ✅ Pattern matching and switch expressions
public PaymentStatus DetermineStatus(PaymentResult result) => result switch
{
    { Success: true, Amount: > 0 } => PaymentStatus.Completed,
    { Success: false, ErrorCode: "INSUFFICIENT_FUNDS" } => PaymentStatus.InsufficientFunds,
    _ => PaymentStatus.Failed
};
```

### Error Handling

```csharp
// ✅ Use Result<T> pattern for domain operations
public class PaymentService
{
    public async Task<Result<Payment, PaymentError>> ProcessPaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Domain logic here
            return Result<Payment, PaymentError>.Success(payment);
        }
        catch (PaymentValidationException ex)
        {
            return Result<Payment, PaymentError>.Failure(
                PaymentError.ValidationFailed(ex.Message));
        }
    }
}

// ❌ Don't catch generic exceptions in domain logic
// ❌ Don't use exceptions for control flow
```

### Async/Await Patterns

```csharp
// ✅ Always use ConfigureAwait(false) in libraries
public async Task<PaymentResult> ProcessAsync(CancellationToken cancellationToken)
{
    var result = await _httpClient
        .PostAsync(endpoint, content, cancellationToken)
        .ConfigureAwait(false);

    return await DeserializeAsync(result).ConfigureAwait(false);
}

// ✅ Proper cancellation token usage
public async Task<Payment[]> GetPaymentsAsync(
    PaymentQuery query,
    CancellationToken cancellationToken = default)
{
    // Implementation
}
```

---

## Testing Guidelines

### Test Structure (AAA Pattern)

```csharp
[Fact]
public async Task ProcessPayment_ValidRequest_ReturnsSuccess()
{
    // Arrange
    var request = new PaymentRequest
    {
        Amount = Money.FromGbp(100),
        Currency = "GBP"
    };
    var service = new PaymentService(_mockLogger.Object);

    // Act
    var result = await service.ProcessPaymentAsync(request, CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Status.Should().Be(PaymentStatus.Completed);
}
```

### Property-Based Testing (FsCheck)

```csharp
[Property]
public Property PaymentAmount_AlwaysPositive(PositiveInt amount)
{
    return Prop.ForAll<Currency>(currency =>
    {
        var money = Money.Create(amount.Get, currency);
        return money.Amount > 0m;
    });
}
```

### Integration Tests

```csharp
public class PaymentApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task PostPayment_ValidPayload_Returns201()
    {
        // Use Testcontainers for database
        // Test actual HTTP endpoints
        // Verify side effects in database
    }
}
```

### Coverage Requirements

- **Unit Tests**: ≥99% line coverage on Core domain logic
- **Mutation Testing**: ≥95% mutation score (Stryker.NET)
- **Integration Tests**: All API endpoints covered
- **Contract Tests**: All external API interactions (Schemathesis)

---

## Security Requirements

### Secrets Management

```csharp
// ✅ Use IConfiguration and Azure Key Vault
public class PaymentSettings
{
    public string StripeApiKey { get; init; } = string.Empty;
    public string TrueLayerClientSecret { get; init; } = string.Empty;
}

// ❌ Never hardcode secrets
// ❌ Never commit secrets to repository
// ❌ Never log sensitive data
```

### Input Validation

```csharp
// ✅ Validate at API boundaries
public async Task<IResult> CreatePayment(
    [FromBody] CreatePaymentRequest request,
    IValidator<CreatePaymentRequest> validator)
{
    var validationResult = await validator.ValidateAsync(request);
    if (!validationResult.IsValid)
    {
        return Results.BadRequest(validationResult.Errors);
    }

    // Process validated input
}
```

### Logging & Monitoring

```csharp
// ✅ Structured logging with Serilog
_logger.LogInformation(
    "Payment processed for {MerchantId} with amount {Amount}",
    merchantId,
    payment.Amount.ToString());

// ❌ Never log PCI data (card numbers, CVV)
// ❌ Never log personal data without proper masking
```

---

## Performance Standards

### Response Time Requirements

- API endpoints: 95th percentile < 200ms
- Database queries: < 100ms for simple operations
- External API calls: Timeout after 30s with retry logic

### Memory & Resource Management

```csharp
// ✅ Use IAsyncDisposable for resources
public class PaymentProcessor : IAsyncDisposable
{
    private readonly HttpClient _httpClient;

    public async ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}

// ✅ Use object pooling for high-frequency objects
// ✅ Implement proper retry policies with Polly
```

---

## Documentation Requirements

### XML Documentation

```csharp
/// <summary>
/// Processes a payment request through the configured payment orchestrator.
/// </summary>
/// <param name="request">The payment request containing amount and payment method.</param>
/// <param name="cancellationToken">Token to cancel the operation.</param>
/// <returns>
/// A task representing the asynchronous operation, containing either a successful
/// payment result or payment error details.
/// </returns>
/// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
public async Task<Result<Payment, PaymentError>> ProcessPaymentAsync(
    PaymentRequest request,
    CancellationToken cancellationToken)
```

### ADR (Architecture Decision Records)

Document all significant architectural decisions in `DOCS/ADRs/`:
- Technology choices
- Design patterns
- Integration approaches
- Security decisions

---

## Git & Branch Strategy

### Commit Message Format

```
<type>(scope): <description>

<body>

Project conceived and specified by Ram Revanur

🤖 Generated with [Claude Code](https://claude.ai/code)

Co-Authored-By: Claude <noreply@anthropic.com>
```

**Types**: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `security`

### Branch Naming

- `feature/wp{n}-{description}` - Work package implementations
- `fix/{issue-description}` - Bug fixes
- `refactor/{component-name}` - Code refactoring
- `docs/{update-type}` - Documentation updates

---

## Quality Gates

### Pre-Commit Checks

- **Build**: Solution must compile without warnings
- **Tests**: All tests must pass
- **Coverage**: ≥99% line coverage maintained
- **Mutation**: ≥95% mutation score maintained
- **Security**: No critical/high severity issues (Trivy, SonarAnalyzer)
- **Formatting**: Code must pass all analyzers

### CI/CD Pipeline Stages

1. **Build & Test** - Fast feedback (< 5 minutes)
2. **Mutation Testing** - Quality verification (< 15 minutes)
3. **Security Scanning** - Vulnerability assessment
4. **Contract Testing** - API contract verification
5. **Load Testing** - Performance validation

---

## Architecture Patterns

### Clean Architecture

```
Api (Presentation)
    ↓
Core (Application & Domain)
    ↓
Infrastructure (External Concerns)
```

### Domain-Driven Design

- **Entities**: Payment, Merchant, Transaction
- **Value Objects**: Money, PaymentMethod, Address
- **Aggregates**: PaymentAggregate manages payment lifecycle
- **Domain Services**: PaymentProcessor, FxRateCalculator
- **Repositories**: Abstract data access patterns

### CQRS & Event Sourcing

```csharp
// Commands - Write operations
public record ProcessPaymentCommand(
    PaymentId Id,
    Money Amount,
    PaymentMethod Method);

// Queries - Read operations
public record GetPaymentQuery(PaymentId Id);

// Events - Domain events
public record PaymentProcessedEvent(
    PaymentId Id,
    Money Amount,
    DateTimeOffset ProcessedAt);
```

### Integration Patterns

- **Orchestration**: Central payment coordinator
- **Circuit Breaker**: Polly for external service resilience
- **Retry Policies**: Exponential backoff with jitter
- **Idempotency**: All operations must be idempotent
- **Event-Driven**: Webhook delivery with guaranteed delivery

---

## Compliance & Regulatory

### PCI DSS Requirements

- No storage of sensitive card data
- All card processing via PCI-compliant partners (Stripe)
- Secure transmission of payment data
- Regular security assessments

### SPI Compliance

- Transaction monitoring and reporting
- Anti-money laundering (AML) controls
- Customer due diligence (CDD) processes
- Safeguarding of client funds

---

## Development Tools & IDE Setup

### Required Extensions (VS Code/Visual Studio)

- C# DevKit
- SonarLint
- SecurityCodeScan
- GitLens
- REST Client

### Recommended Settings

```json
{
  "editor.formatOnSave": true,
  "csharp.format.enable": true,
  "omnisharp.enableAnalyzersSupport": true,
  "files.trimTrailingWhitespace": true
}
```

---

## Getting Started Checklist

- [ ] Clone repository and review this document
- [ ] Set up development environment (.NET 9 SDK)
- [ ] Run `dotnet build ZEN/RichMove.SmartPay.sln` successfully
- [ ] Run `dotnet test ZEN/RichMove.SmartPay.sln` - all tests pass
- [ ] Set up IDE with required extensions
- [ ] Review architecture documentation in `DOCS/`
- [ ] Understand work package structure in `WPS/`

---

*Last Updated: 2025-09-13*
*Version: 1.0 (Post WP1 Completion)*