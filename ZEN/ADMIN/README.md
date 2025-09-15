# SmartPay Admin (WP6)

Minimal Blazor WebAssembly admin app for testing SmartPay APIs and demonstrating WP3 payment functionality.

## Features

- **Health Check**: Test API connectivity with `/health/ready` endpoint
- **FX Quotes**: Create foreign exchange quotes via `/api/fx/quote`
- **Payment Intents**: Create payment intents using WP3 mock provider via `/api/payments/intent`
- **Bootstrap UI**: Responsive, modern interface with loading states

## Running Locally

### Prerequisites
- .NET 8.0 SDK
- SmartPay API running on http://localhost:5001

### Start the Admin App

```bash
cd ZEN/ADMIN/SmartPay.AdminBlazor
dotnet run
```

The app will be available at http://localhost:5000

### Configuration

Configure the API base URL in `wwwroot/appsettings.json`:

```json
{
  "ApiBaseUrl": "http://localhost:5001"
}
```

### Using the App

1. **Health Check**: Click "Check /health/ready" to verify API connectivity
2. **FX Quote**: Enter an amount and click "Create Quote" to test FX functionality
3. **Payment Intent**: Enter currency, amount, and reference to test WP3 payment provider

## Development

The app is structured as a minimal Blazor WebAssembly application:

- `Pages/Index.razor` - Main dashboard page
- `MainLayout.razor` - Layout with sidebar navigation
- `Program.cs` - App configuration and DI setup
- `wwwroot/` - Static files and configuration

## Architecture

This admin app demonstrates the WP6 pattern:
- **Client-side Blazor** for fast, interactive UI
- **HTTP client** configured for API communication
- **Bootstrap** for responsive styling
- **Error handling** with user-friendly messages
- **Loading states** for better UX