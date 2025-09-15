# WP06 — Checkout UI and SDKs

## Overview
Develops lightning-fast server-rendered Blazor admin/merchant console and auto-generated typed SDKs from OpenAPI specifications, providing optimal user experience and seamless developer integration capabilities.

## Epic and Feature Coverage
- **E7 (Blazor SSR Admin UI & SDK)**: Complete UI and SDK implementation
  - E7.F1.C1 (Real-time Data Synchronization): Live UI updates via SignalR
  - E7.F1.C2 (Live Notifications): Multi-channel notification system
  - E7.F2.C1 (Complete API Coverage): SDK generation from OpenAPI
  - E7.F2.C2 (Authentication Integration): Multi-auth method support
  - E7.F2.C3 (Advanced SDK Features): Idempotency and retry logic

## Business Objectives
- **Primary**: Deliver sub-300ms TTFB and <1.2s FCP for optimal user experience
- **Secondary**: Enable rapid developer integration through typed SDKs
- **Tertiary**: Provide real-time operational visibility for merchants

## Technical Scope

### Features Planned
1. **Blazor Server-Side Rendering (SSR)**
   - Ultra-fast server-rendered pages with interactive islands
   - Real-time updates via SignalR integration
   - Tenant-aware theming and customization
   - Performance optimization for sub-300ms TTFB

2. **SDK Generation Pipeline**
   - Automated C# and TypeScript client generation from OpenAPI
   - NuGet and npm package publishing automation
   - Comprehensive authentication method support
   - Advanced features (idempotency, retry logic, error handling)

3. **Real-time User Interface**
   - Live table updates for quotes, payments, and transactions
   - Toast notifications for system events
   - Collaborative features with user presence indicators
   - Offline capability with sync on reconnection

4. **Administrative Dashboard**
   - Comprehensive CRUD operations for all entities
   - Advanced search and filtering capabilities
   - Role-based access control enforcement
   - Real-time KPI dashboards and analytics

### UI Architecture Pattern
```
[Browser] ⇄ HTTPS
   ↳ SSR HTML (Blazor) + minimal interactivity
   ↳ WebSocket (SignalR) for tenant events
[Blazor SSR] —uses→ [REST API + OpenAPI] —emits→ [Webhooks + SignalR]
```

### Performance Targets
- **TTFB**: <300ms (p95) for all pages
- **FCP**: <1.2s for first contentful paint
- **HTML Payload**: <40KB initial page load
- **Interactive Islands**: 1 RTT to interactivity
- **SignalR Connection**: <1s connection establishment

## Implementation Details

### Tasks Planned
1. ⏳ Implement Blazor SSR shell with layout, auth, and navigation
2. ⏳ Build real-time SignalR integration for live updates
3. ⏳ Create OpenAPI hardening and SDK generation pipeline
4. ⏳ Develop key pages (Quotes, Rates, Tenants, Users)
5. ⏳ Add pagination, virtualization, and output caching
6. ⏳ Implement authentication and tenant-aware theming
7. ⏳ Performance optimization and cost analysis

### Commit Points
- `feat(wp6): blazor ssr shell + auth` - Core UI foundation
- `feat(wp6): signalr realtime + live updates` - Real-time capabilities
- `feat(wp6): sdk generation + publishing` - Developer experience
- `feat(wp6): admin pages + crud operations` - Administrative functionality
- `perf(wp6): optimization + caching` - Performance enhancements

### Requirements Traceability
| Requirement ID | Description | Verification Method | Status |
|---|---|---|---|
| E7.F1.C1.R1 | Instant UI updates on data changes (<500ms) | Performance testing | ⏳ PLANNED |
| E7.F1.C2.R1 | Multi-channel notification delivery | Integration testing | ⏳ PLANNED |
| E7.F2.C1.R1 | Complete API coverage in SDKs | SDK testing | ⏳ PLANNED |
| E7.F2.C2.R1 | Multiple authentication method support | Security testing | ⏳ PLANNED |
| E7.F2.C3.R1 | Idempotency-key support | Integration testing | ⏳ PLANNED |

### Test Coverage Targets
- **Unit Tests**: 90% coverage on UI components and SDK logic
- **Integration Tests**: 85% coverage on API integrations
- **Performance Tests**: TTFB and FCP targets validated
- **UI Tests**: Playwright for cross-browser compatibility
- **SDK Tests**: Generated client functionality verified

## Definition of Done
- [ ] Blazor SSR application with sub-300ms TTFB performance
- [ ] Real-time UI updates working via SignalR integration
- [ ] C# and TypeScript SDKs auto-generated and published
- [ ] Administrative pages for all core entities operational
- [ ] Role-based access control enforced throughout UI
- [ ] Performance targets met (TTFB <300ms, FCP <1.2s)
- [ ] Cross-browser compatibility verified
- [ ] SDK documentation and examples provided

## Blazor SSR Architecture

### Page Rendering Patterns
```razor
@* Pattern A: Read-only list (fastest path) *@
<QuoteList InitialItems="Model.Quotes" RenderMode="InteractiveOnDemand" />

@* Pattern B: Interactive forms *@
<QuoteForm RenderMode="InteractiveAuto" OnSubmit="HandleQuoteSubmission" />

@* Pattern C: Real-time table *@
<PaymentTable @ref="paymentTable" RenderMode="InteractiveServer" />
```

### Performance Optimization Strategy
1. **SSR + Streaming**: Render head + above-fold first, stream content
2. **Interactive Islands**: Use minimal JavaScript for specific components
3. **Output Caching**: Cache stable data with ETags (5-15 minutes)
4. **Response Compression**: Gzip/Brotli for payload optimization
5. **Virtualization**: Server-side paging with `Virtualize` component
6. **Asset Optimization**: Inline critical CSS, defer non-critical resources

### Real-time Integration
```csharp
@implements IAsyncDisposable
@inject IJSRuntime JS

<div id="notification-container">
    @foreach (var notification in notifications)
    {
        <div class="alert alert-@notification.Type">@notification.Message</div>
    }
</div>

@code {
    private HubConnection? hubConnection;
    private List<Notification> notifications = new();

    protected override async Task OnInitializedAsync()
    {
        hubConnection = new HubConnectionBuilder()
            .WithUrl("/hubs/notifications")
            .Build();

        hubConnection.On<FxQuoteCreatedEvent>("fx.quote.created", HandleQuoteCreated);
        await hubConnection.StartAsync();
    }

    private async Task HandleQuoteCreated(FxQuoteCreatedEvent quote)
    {
        notifications.Add(new Notification("success", $"New quote created: {quote.Id}"));
        await InvokeAsync(StateHasChanged);
    }
}
```

## SDK Generation Pipeline

### OpenAPI Enhancement
```yaml
# Enhanced OpenAPI specification
openapi: 3.0.3
info:
  title: SmartPay API
  version: 1.0.0
  description: Complete payment and FX platform API
paths:
  /api/fx/quote:
    post:
      summary: Generate FX quote
      operationId: createFxQuote
      x-idempotent: true
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/FxQuoteRequest'
      responses:
        '200':
          $ref: '#/components/responses/FxQuoteResponse'
        '400':
          $ref: '#/components/responses/ValidationError'
```

### C# SDK Usage Example
```csharp
var client = new SmartPayClient(baseUrl, credentials, tenantId: "blue");

// Idempotent quote creation
var quote = await client.Fx.CreateQuoteAsync(new FxQuoteRequest
{
    FromCurrency = "USD",
    ToCurrency = "GBP",
    Amount = 1000m
}, idempotencyKey: Guid.NewGuid().ToString("N"));

// Automatic retry with exponential backoff
var payment = await client.Payments.ProcessAsync(paymentRequest,
    retryPolicy: RetryPolicy.ExponentialBackoff(maxRetries: 3));
```

### TypeScript SDK Usage Example
```typescript
import { SmartPayClient } from "@richmove/smartpay";

const client = new SmartPayClient({
    baseUrl: "https://api.smartpay.com",
    apiKey: "your-api-key",
    tenant: "blue"
});

// Promise-based with TypeScript types
const quote = await client.fx.createQuote({
    fromCurrency: "USD",
    toCurrency: "GBP",
    amount: 1000
});

// WebSocket real-time updates
client.notifications.on('fx.quote.created', (event) => {
    console.log('New quote:', event.quote);
});
```

### SDK Publishing Automation
```yaml
# GitHub Actions workflow
name: SDK Generation and Publishing
on:
  push:
    paths: ['src/Api/**', 'openapi.json']

jobs:
  generate-sdks:
    runs-on: ubuntu-latest
    steps:
      - name: Generate C# SDK
        run: nswag run nswag.json
      - name: Generate TypeScript SDK
        run: openapi-generator generate -i openapi.json -g typescript
      - name: Publish to NuGet
        run: dotnet nuget push *.nupkg
      - name: Publish to npm
        run: npm publish
```

## Administrative Dashboard Features

### Core Entity Management
1. **Quote Management**
   - Real-time quote list with filtering and search
   - Quote details view with conversion history
   - Manual quote adjustment capabilities
   - Export functionality for reporting

2. **Payment Processing**
   - Payment transaction monitoring
   - Status tracking and investigation tools
   - Retry and reconciliation capabilities
   - Dispute management workflow

3. **User and Tenant Administration**
   - User role management and permissions
   - Tenant configuration and settings
   - Activity monitoring and audit logs
   - Bulk operations for administrative efficiency

4. **Financial Operations**
   - Rate management and overrides
   - Settlement and reconciliation monitoring
   - Financial reporting and analytics
   - Risk exposure tracking

### Advanced UI Features
- **Data Virtualization**: Handle large datasets efficiently
- **Advanced Filtering**: Multi-column, multi-criteria filtering
- **Bulk Operations**: Select and operate on multiple records
- **Export Capabilities**: CSV, Excel, PDF export options
- **Responsive Design**: Mobile and tablet optimization
- **Accessibility**: WCAG 2.1 AA compliance

## Security and Tenant Isolation

### Authentication Methods
- **Cookie-based**: For Blazor SSR UI sessions
- **JWT Bearer**: For API integrations
- **OAuth2**: Client credentials and authorization code flows
- **API Keys**: For server-to-server integrations

### Tenant Security Implementation
```csharp
[Authorize]
public class QuotesController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetQuotes()
    {
        var tenantId = TenantContext.Current?.TenantId;
        var quotes = await _quoteService.GetQuotesAsync(tenantId);
        return Ok(quotes);
    }
}
```

### Role-Based Access Control
- **Owner**: Full access to tenant resources
- **Admin**: Administrative access within tenant
- **Operator**: Operational access for daily tasks
- **Viewer**: Read-only access for reporting
- **Support**: Limited access for customer support

## Cost-Optimized Azure Deployment

### Deployment Options
1. **Azure Container Apps**: Scale-to-zero for cost optimization
2. **Azure App Service**: Simple deployment with managed infrastructure
3. **Static Web Apps**: Cost-effective for marketing and documentation

### Cost Management
- **Resource Right-sizing**: Optimize compute and memory allocation
- **Auto-scaling**: Scale based on actual demand
- **CDN Integration**: Reduce bandwidth costs
- **Caching Strategy**: Minimize compute requirements
- **Development Environment**: Auto-shutdown during off-hours

## Dependencies
- **Upstream**: WP01 (Repository & Tooling), WP03 (API & Contracts), WP04 (Payment Orchestrator), WP05 (FX & Remittance SPI)
- **Downstream**: WP07 (Merchant Dashboard), WP08 (Analytics & Reporting)

## Minimal Admin UI Implementation (WP6)

### Scope
- Minimal Admin app (Blazor WebAssembly for now; SSR swap optional later) to verify APIs and demos.
- OpenAPI-first SDK scaffolding (TS/C#) via CLI tools.

### Running Admin (local)
```bash
cd ADMIN/SmartPay.AdminBlazor
dotnet run
```
Set `ApiBaseUrl` via environment or `wwwroot/appsettings.json` if needed.

## V&V {#vv}
### Feature → Test mapping
| Feature ID | Name | Test IDs | Evidence / Location |
|-----------:|------|----------|---------------------|
| E7.F1 | Admin UI (minimal) | SMK-E7-UI-Health | Admin calls `/health/ready` |
| E7.F1.N1 | Create quote from UI | SMK-E7-UI-Quote | Admin posts to `/api/fx/quote` |
| E7.F2 | SDK generation scaffold | PLAN-E7-SDK | `TOOLS/SDK/README.md` |
| E7.F3 | Blazor SSR UI | SMK-E7-Blazor, PERF-E7-TTFB | Smoke_Features.md §3.7-A/B |
| E7.F4 | SDK Generation | SMK-E7-SDK, INTEG-E7-Client | Integration tests (SDK) |
| E7.F5 | Real-time Updates | SMK-E7-SignalR, INTEG-E7-Live | Real-time tests |
| E7.F6 | Performance Targets | PERF-E7-FCP, LOAD-E7-Scale | Performance tests |

### Acceptance
- Admin calls health endpoints and creates quotes; SDK generation documented.
- Blazor UI renders <1.2s FCP; SDKs auto-generate from OpenAPI; real-time updates functional.

### Rollback
- Fallback to static HTML forms; API-only mode for SDK consumers.

## Risk Assessment
- **Technical Risk**: MEDIUM (new Blazor SSR technology)
- **Business Risk**: HIGH (core user interface)
- **Performance Risk**: MEDIUM (strict performance requirements)
- **Mitigation**: Performance testing, gradual rollout, fallback strategies

---
**Status**: ⏳ PLANNED
**Last Updated**: 2025-09-14
**Owner**: Frontend Team + DevEx Team
**Next Phase**: WP07 - Merchant Dashboard