# API Contract Standards

## JSON Security Guidelines

### Fail Closed on Unknown Fields

**FastEndpoints Best Practice:** Configure strict JSON parsing for new endpoints:

```csharp
// In endpoint configuration
public override void Configure()
{
    Post("/api/v1/wallets");
    SerializerOptions(opt => opt.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
}
```

**Benefits:**
- Prevents accidental data exposure
- Catches client bugs early
- Enforces contract discipline
- Security-first approach

## DTO Immutability Guidelines

### Record Types for API Contracts

**Preferred:** Use `record` types for all API request/response DTOs to ensure immutability:

```csharp
// ✅ Preferred - Immutable record
public sealed record CreateWalletRequest(
    string Address,
    string Currency,
    decimal? InitialBalance = null);

// ✅ Preferred - Immutable response record
public sealed record CreateWalletResponse(
    string WalletId,
    DateTime CreatedAt);
```

**Avoid:** Mutable classes with setters:

```csharp
// ❌ Avoid - Mutable class
public sealed class CreateWalletRequest
{
    public string Address { get; set; } = "";
    public string Currency { get; set; } = "";
    public decimal? InitialBalance { get; set; }
}
```

### Benefits of Record Types

1. **Immutability by default** - Prevents accidental mutations
2. **Value equality** - Structural comparison instead of reference
3. **Concise syntax** - Less boilerplate code
4. **Thread safety** - Immutable objects are inherently thread-safe
5. **Better testing** - Predictable equality comparisons

### Migration Strategy

- **New APIs:** Always use record types
- **Existing APIs:** Consider record conversion during major version updates
- **No breaking changes:** Keep existing classes until v2.0+

### JSON Serialization

Records work seamlessly with System.Text.Json:

```csharp
// Automatic serialization/deserialization
var request = new CreateWalletRequest("0x123", "USD", 100.0m);
var json = JsonSerializer.Serialize(request);
var deserialized = JsonSerializer.Deserialize<CreateWalletRequest>(json);
```