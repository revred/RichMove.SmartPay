# RichMove.SmartPay

[![Build Status](https://github.com/ramrevanur/richmove-smartpay/workflows/ci-coverage/badge.svg)](https://github.com/ramrevanur/richmove-smartpay/actions)
[![Coverage Gate](https://img.shields.io/badge/coverage-60%25%20min-brightgreen)](https://github.com/ramrevanur/richmove-smartpay/actions)

**Payment orchestration platform conceived and specified by Ram Revanur**

This repository powers the SmartPay API and related admin/merchant UI.

## Current Focus
- **WP4**: Realtime notifications, multi‑tenancy scaffolding, lightweight analytics.
- **WP5**: Event Bridge (webhooks) + RLS templates.
- **WP6**: UI & SDK plan (Blazor SSR + REST‑first SDKs). This commit adds **documentation only** for WP6.

## Quick Links
- **WP4**: `WPS/WP04_Payment_Orchestrator_and_Connectors_V2.md`
- **WP6 Plan**: `WPS/WP06_Checkout_UI_and_SDKs_V2.md`
- **Blazor UI Plan**: `DOCS/UI/Blazor/Plan.md`
- **Low‑Cost Azure Hosting**: `DOCS/Hosting/Azure.LowCost.md`
- **SDK Consumption**: `DOCS/API/SDK-Consumption.md`
- **Blazor Perf Cookbook**: `DOCS/Perf/Blazor.Fast.md`

## V&V Hub
- **WPS Index**: `WPS/INDEX.md`
- **Traceability Matrix**: `DOCS/VnV/TraceabilityMatrix.csv`
- **Verification Process**: `DOCS/VnV/VerificationAndValidation.md`
- **Smoke Playbook**: `Smoke_Features.md`

### MVP Guardrail
- This repo tracks some advanced infrastructure (K8s/Prometheus/Scaling, Blockchain stubs). By default they're **OFF** via feature flags.
- A **narrow allowlist** is **MVP-optional** when guardrails are on:
  - `/metrics` bound privately + admin auth
  - `/scaling/status` admin auth; no PII
- Everything else stays parked under **WP8** until promoted via V&V.

## 🏗️ Architecture

- **Clean Architecture** with clear separation of concerns
- **Domain-Driven Design** principles
- **FastEndpoints** for high-performance API development
- **Supabase** integration for data persistence and real-time features
- **Conditional DI** for flexible deployment scenarios

## 🚀 Features

### WP1: Foundation
- ✅ FastEndpoints integration with dual Swagger support
- ✅ Comprehensive CI/CD pipeline with 60% test coverage enforcement
- ✅ Clean Architecture setup with Core/Infrastructure/API separation
- ✅ Health checks and monitoring endpoints

### WP2: Supabase Integration
- ✅ Foreign exchange quote generation with real-time pricing
- ✅ Persistent quote storage in Supabase PostgreSQL
- ✅ Dynamic pricing updates via background services
- ✅ Database health monitoring at `/v1/health/db`
- ✅ Conditional deployment (Supabase vs in-memory fallback)

## 🛠️ Technology Stack

- **.NET 9.0** with C# 13
- **FastEndpoints 6.1** for API development
- **Supabase** (PostgreSQL 17) for data persistence
- **Npgsql** for database connectivity
- **xUnit** with FluentAssertions for testing
- **GitHub Actions** for CI/CD
- **Testcontainers** for integration testing

## 📦 Project Structure

```
ZEN/
├── SOURCE/
│   ├── Api/                    # FastEndpoints API layer
│   │   ├── Endpoints/          # API endpoints organized by feature
│   │   └── Program.cs          # Application bootstrap
│   ├── Core/                   # Domain logic and interfaces
│   │   └── ForeignExchange/    # FX domain models and interfaces
│   └── Infrastructure/         # Data access and external services
│       ├── Data/               # Data configuration classes
│       ├── ForeignExchange/    # FX implementation services
│       └── Supabase/           # Supabase-specific implementations
├── TESTS/                      # Test projects
├── SUPABASE/                   # Supabase configuration and migrations
└── Directory.Packages.props    # Central package management
```

## 🔧 Getting Started

### Prerequisites

- .NET 9.0 SDK
- Supabase account (optional - fallback mode available)

### Local Development

1. **Clone the repository:**
   ```bash
   git clone https://github.com/ramrevanur/richmove-smartpay.git
   cd richmove-smartpay
   ```

2. **Configure secrets (optional for Supabase integration):**
   ```bash
   # Copy sample secrets
   cp secrets/SMARTPAY-RED.secrets.env.sample secrets/SMARTPAY-RED.secrets.env
   # Edit with your Supabase credentials
   ```

3. **Run the application:**
   ```bash
   cd ZEN
   dotnet run --project SOURCE/Api
   ```

4. **Access the API:**
   - Swagger UI: `https://localhost:5001/swagger`
   - Health checks: `https://localhost:5001/health/live`
   - Database health: `https://localhost:5001/v1/health/db`

### Configuration Modes

#### Supabase Mode (Production)
```json
{
  "Supabase": {
    "Enabled": true,
    "Url": "https://your-project.supabase.co",
    "Key": "your-anon-key"
  }
}
```

#### Fallback Mode (Development)
```json
{
  "Supabase": {
    "Enabled": false
  },
  "FX": {
    "Pricing": {
      "MarkupBps": 25,
      "FixedFeeMinorUnits": 99
    }
  }
}
```

## 📊 API Endpoints

### Core Services
- `GET /` - Service information
- `GET /health/live` - Liveness check
- `GET /health/ready` - Readiness check
- `GET /v1/health/db` - Database connectivity (when Supabase enabled)

### Foreign Exchange
- `POST /api/fx/quote` - Generate FX quote
  ```json
  {
    "fromCurrency": "USD",
    "toCurrency": "GBP",
    "amount": 1000
  }
  ```

### Shop Integration
- `GET /api/shop/info` - Shop information (placeholder for future development)

## 🧪 Testing

### Run Tests
```bash
dotnet test
```

### Coverage Reports
```bash
dotnet test --collect:"XPlat Code Coverage"
```

The CI/CD pipeline enforces a minimum 60% test coverage threshold.

## 🚀 Deployment

### GitHub Actions

The project includes a comprehensive CI/CD pipeline:

- **Build verification** on all PRs and pushes to main branches
- **Test execution** with coverage collection
- **Coverage enforcement** (60% minimum threshold)
- **Artifact upload** for coverage reports

### Supabase Setup

1. Create a Supabase project
2. Run the included migrations from `ZEN/SUPABASE/migrations/`
3. Configure environment variables with your Supabase credentials

## 📈 Roadmap

### WP3: Payment Provider Integration
- Multi-provider payment processing
- Provider failover and routing
- Transaction state management

### WP4: Advanced Features ✅
- ✅ Real-time notifications with SignalR
- ✅ Multi-tenant scaffolding with tenant context
- ✅ Lightweight analytics and request logging
- ✅ Event-driven FX quote triggers (WP4.1)

### WP6: UI & SDK (Plan)
- **Blazor SSR UI**: Lightning-fast server-rendered admin console
- **REST-first SDKs**: Auto-generated C# and TypeScript clients from OpenAPI
- **Performance targets**: TTFB < 300ms, FCP < 1.2s on low-end devices
- **Ultra-low cost Azure hosting**: Scale-to-zero with Container Apps or minimal App Service

## 🤝 Contributing

- Prefer **OpenAPI‑first** for new endpoints.
- Keep UI **server‑rendered** and minimal; use realtime only where it adds value.
- Track performance budgets; fail builds on regressions.

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Ensure tests pass and coverage meets threshold
5. Submit a pull request

## 📄 License

This project is proprietary software conceived and specified by Ram Revanur.

## 🎯 Development Philosophy

This project follows a **"Red-First"** development strategy:
- **RED environment**: Free Supabase tier for development and testing
- **GREEN environment**: Paid tier for production workloads
- **Cost-conscious scaling**: Automatic tier management based on usage

Built with ❤️ by the RichMove.SmartPay team