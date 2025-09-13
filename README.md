# RichMove.SmartPay

[![Build Status](https://github.com/ramrevanur/richmove-smartpay/workflows/ci-coverage/badge.svg)](https://github.com/ramrevanur/richmove-smartpay/actions)
[![Coverage Gate](https://img.shields.io/badge/coverage-60%25%20min-brightgreen)](https://github.com/ramrevanur/richmove-smartpay/actions)

**Payment orchestration platform conceived and specified by Ram Revanur**

A modern, scalable payment orchestration platform built with .NET 9, FastEndpoints, and Supabase integration for seamless foreign exchange operations and multi-provider payment processing.

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

### WP4: Advanced Features
- Real-time notifications
- Advanced analytics dashboard
- Multi-tenant support

## 🤝 Contributing

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