# SmartPay TypeScript SDK (Skeleton)

> **Status**: Skeleton implementation for future development

## Goals
- Typed client with full API coverage
- Automatic retries with exponential backoff
- ProblemDetails error mapping
- Idempotency helper with automatic key generation
- Built-in correlation tracking

## Quick Start (Future)

```typescript
import { SmartPayClient } from '@richmove/smartpay-sdk';

const client = new SmartPayClient({
  baseUrl: 'https://api.richmove.co.uk',
  timeout: 5000,
  retries: 3
});

// Get FX quote with automatic idempotency
const quote = await client.fx.getQuote({
  baseCurrency: 'GBP',
  quoteCurrency: 'EUR',
  amount: 1250.50,
  clientId: 'demo-client'
});

console.log(`Rate: ${quote.rate}, Expires: ${quote.expiresAtUtc}`);
```

## Basic Implementation Skeleton

```typescript
// Types from OpenAPI schema
export interface FxQuoteRequest {
  baseCurrency: string;
  quoteCurrency: string;
  amount: number;
  clientId?: string;
  correlationId?: string;
}

export interface FxQuoteResult {
  rate: number;
  baseCurrency: string;
  quoteCurrency: string;
  amount: number;
  fees?: number;
  expiresAtUtc: string;
  provider?: string;
}

export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail?: string;
  traceId?: string;
}

// Basic client with idempotency
export class SmartPayClient {
  constructor(private config: ClientConfig) {}

  async getQuote(request: FxQuoteRequest): Promise<FxQuoteResult> {
    const headers = {
      'Content-Type': 'application/json',
      'Idempotency-Key': request.correlationId || crypto.randomUUID(),
      'User-Agent': 'SmartPaySDK/1.0.0'
    };

    const response = await this.fetchWithRetry('/api/v1/fx/quote', {
      method: 'POST',
      headers,
      body: JSON.stringify(request)
    });

    if (!response.ok) {
      const problem: ProblemDetails = await response.json();
      throw new SmartPayError(problem);
    }

    return await response.json();
  }

  private async fetchWithRetry(url: string, options: RequestInit, attempts = 0): Promise<Response> {
    // Implement exponential backoff retry logic
    // Handle 429 Retry-After headers
    // Respect timeout settings
    throw new Error('Not implemented yet');
  }
}

export class SmartPayError extends Error {
  constructor(public problem: ProblemDetails) {
    super(problem.title || 'SmartPay API Error');
    this.name = 'SmartPayError';
  }
}
```

## Development Plan
1. **Phase 1**: Basic types from OpenAPI
2. **Phase 2**: Client with retries and error handling
3. **Phase 3**: Advanced features (caching, webhooks)
4. **Phase 4**: Framework integrations (React hooks, etc.)

## Testing Strategy
- Unit tests for client logic
- Integration tests against live API
- Contract tests against OpenAPI schema
- Mock server for development

## Publishing
- NPM package: `@richmove/smartpay-sdk`
- Automatic versioning from API changes
- TypeScript definitions included
- ESM + CommonJS builds