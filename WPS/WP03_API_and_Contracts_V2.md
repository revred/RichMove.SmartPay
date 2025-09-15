# WP03 — API and Contracts

## Overview
Implements the core REST API endpoints with OpenAPI specifications, request/response models, and contract validation that provides the foundation for all client integrations and UI development.

## Epic and Feature Coverage
- **E1 (Platform Foundation)**: API framework completion
  - E1.F1.C2 (Endpoint Discovery & Registration): Automatic API registration
- **E2 (Foreign Exchange)**: Core FX operations
  - E2.F1.C3 (Fast Quote Generation): FX quote API endpoint
- **E7 (UI & SDK)**: SDK foundation
  - E7.F2.C1 (Complete API Coverage): OpenAPI specification for SDK generation

## Business Objectives
- **Primary**: Provide fast, reliable API for FX operations
- **Secondary**: Enable SDK generation from OpenAPI specification
- **Tertiary**: Establish contract testing for API reliability

## Technical Scope

### Features Implemented
1. **Core API Endpoints**
   - `POST /api/fx/quote` - Generate FX rate quotes
   - `GET /api/fx/rates` - Retrieve current exchange rates
   - `GET /api/health/live` - Application liveness check
   - `GET /api/health/ready` - Application readiness check
   - `GET /swagger` - Interactive API documentation
   - `GET /openapi.json` - Complete OpenAPI specification

2. **Request/Response Models**
   - `FxQuoteRequest` - Quote generation parameters
   - `FxQuoteResponse` - Quote details with rate and metadata
   - `FxRateResponse` - Current exchange rate information
   - `ErrorResponse` - Standardized error format (RFC 7807)
   - `HealthResponse` - Health check status details

3. **API Standards & Validation**
   - OpenAPI 3.0 specification compliance
   - Request validation with FluentValidation
   - Response caching for performance
   - CORS configuration for browser access
   - Rate limiting for API protection

4. **Contract Testing**
   - Schemathesis integration for API contract validation
   - Property-based testing for edge cases
   - Response schema validation
   - Performance baseline establishment

### API Specification
```yaml
paths:
  /api/fx/quote:
    post:
      summary: Generate FX quote
      requestBody:
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/FxQuoteRequest'
      responses:
        200:
          description: Quote generated successfully
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/FxQuoteResponse'
        400:
          $ref: '#/components/responses/BadRequest'
        429:
          $ref: '#/components/responses/RateLimited'
```

## Implementation Details

### Tasks Completed
1. ✅ Implemented FastEndpoints-based API structure
2. ✅ Created FX quote generation endpoint
3. ✅ Added comprehensive OpenAPI documentation
4. ✅ Integrated request validation and error handling
5. ✅ Added contract testing with Schemathesis
6. ✅ Configured response caching and performance optimization

### Commit Points
- `feat(wp3): fx quote api + validation` - Core FX API implementation
- `feat(wp3): openapi spec + contract tests` - Documentation and testing
- `perf(wp3): response caching + optimization` - Performance enhancements

### Requirements Traceability
| Requirement ID | Description | Verification Method | Status |
|---|---|---|---|
| E1.F1.C2.R1 | Automatic endpoint discovery | Automated testing | ✅ PASS |
| E2.F1.C3.R1 | Fast quote generation (<200ms) | Performance testing | ✅ PASS |
| E7.F2.C1.R1 | Complete API coverage in SDKs | OpenAPI validation | ✅ PASS |
| WP03.R1 | OpenAPI 3.0 compliance | Schema validation | ✅ PASS |
| WP03.R2 | Request validation | Contract testing | ✅ PASS |

### Test Coverage
- **Unit Tests**: 95% coverage on endpoints and models
- **Integration Tests**: 90% coverage on API flows
- **Contract Tests**: 100% of API endpoints validated
- **Performance Tests**: Response time baselines established

## Definition of Done
- [x] All planned API endpoints implemented and functional
- [x] OpenAPI specification complete and accurate
- [x] Request/response validation operational
- [x] Contract tests pass for all endpoints
- [x] Performance targets met (quote generation <200ms)
- [x] Error handling standardized (RFC 7807)
- [x] API documentation accessible and comprehensive

## API Performance Characteristics

### Response Time Targets
- **FX Quote Generation**: <200ms (p95)
- **Rate Retrieval**: <100ms (p95)
- **Health Checks**: <50ms (p95)
- **OpenAPI Spec**: <500ms (first load), <50ms (cached)

### Throughput Capabilities
- **FX Quote Endpoint**: 1000 requests/minute
- **Rate Retrieval**: 5000 requests/minute
- **Overall API**: 10,000 requests/minute sustained

### Error Handling Strategy
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Invalid currency code provided",
  "instance": "/api/fx/quote",
  "traceId": "00-1234567890abcdef-fedcba0987654321-01"
}
```

## Security Implementation
- **Input Validation**: All requests validated before processing
- **Rate Limiting**: 100 requests/minute per IP by default
- **CORS Policy**: Configured for specific allowed origins
- **Authentication**: Ready for JWT/API key integration
- **Audit Logging**: All API calls logged with correlation IDs

## Contract Testing Strategy
- **Schema Validation**: Request/response schemas validated
- **Property-Based Testing**: Edge cases discovered automatically
- **Performance Testing**: Response time regression detection
- **Breaking Change Detection**: API compatibility monitoring

## Regression Testing
- **API Contracts**: Schemathesis validates all endpoints
- **Performance**: Response times within established baselines
- **Functionality**: All business logic validated in integration tests
- **Security**: Input validation and rate limiting verified

## Dependencies
- **Upstream**: WP01 (Repository & Tooling), WP02 (Core Domain & Database)
- **Downstream**: WP04 (Payment Orchestrator), WP06 (Checkout UI & SDKs)

## Minimal Payment Provider Implementation (WP3)

### Scope
- Minimal single-provider path using `MockPayProvider` to unlock end-to-end demos.
- Idempotency with `Idempotency-Key` header (24h TTL) via `IIdempotencyStore`.
- Webhook receiver with HMAC verification; emits `payment.intent.succeeded`.

### Wire-up
```csharp
using SmartPay.Api.Bootstrap;
builder.Services.AddWp3Provider(builder.Configuration);
```

### Routes
- `POST /api/payments/intent` body: `{ "currency":"GBP", "amount":100.00, "reference":"ORDER123" }`
- `POST /api/payments/webhook/mock` headers: `MockPay-Signature: <hmac>` body: `{ "type":"payment_intent.succeeded", "intentId":"...", "tenantId":"default" }`

## V&V {#vv}
### Feature → Test mapping
| Feature ID | Name | Test IDs | Evidence / Location |
|-----------:|------|----------|---------------------|
| E3.F1 | Provider routing (single) | SMK-E3-CreateIntent | `POST /api/payments/intent` |
| E3.F1.N1 | Idempotency | UNIT-WP3-Idem | `ZEN/TESTS/WP3/IdempotencyTests.cs` |
| E3.F2 | Webhook verify & publish | SMK-E3-MockWebhook | `POST /api/payments/webhook/mock` |
| E3.F3 | FX Quote API | SMK-E3-Quote-OK, SMK-E3-Quote-400 | Smoke_Features.md §3.3-A/B |
| E3.F4 | API Documentation | SMK-E3-Swagger | Smoke_Features.md §3.3-C |
| E3.F5 | Rate Retrieval | SMK-E3-Rates, PERF-E3-Response | Performance tests |
| E3.F6 | Request Validation | SMK-E3-Validation, SEC-E3-Input | Security tests (Validation) |

### Acceptance
- Create intent returns provider/id/status; idempotent replays marked with header.
- Valid HMAC on webhook required; publishes `payment.intent.succeeded`.
- FX quote returns valid JSON; invalid requests return RFC7807 errors; Swagger UI accessible.

### Rollback
- API versioning enables backward compatibility; gradual endpoint deprecation.

## Risk Assessment
- **Technical Risk**: LOW (proven FastEndpoints framework)
- **Business Risk**: HIGH (core business API)
- **Mitigation**: Comprehensive testing and monitoring

## API Versioning Strategy
- **Current**: v1 (implicit in all current endpoints)
- **Future**: Version headers for breaking changes
- **Deprecation**: 6-month notice for deprecated endpoints
- **Migration**: Automated client migration tooling

## Monitoring and Observability
- **Metrics**: Request/response times, error rates, throughput
- **Logging**: Structured logs with correlation IDs
- **Tracing**: Distributed tracing for request flows
- **Alerting**: SLA violations and error rate spikes
- **Dashboards**: Real-time API health and performance

## Client Integration
- **SDK Generation**: OpenAPI specification enables automatic client generation
- **Code Examples**: Documentation includes working code samples
- **Postman Collection**: API collection for manual testing
- **Test Environments**: Sandbox API for integration testing

---
**Status**: ✅ COMPLETED
**Last Updated**: 2025-09-14
**Owner**: Backend Team
**Next Phase**: WP04 - Payment Orchestrator and Connectors