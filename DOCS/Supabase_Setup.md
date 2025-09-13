# Supabase Setup Documentation

## Overview

This document outlines the Supabase setup for the RichMove.SmartPay platform. Supabase provides the backend-as-a-service infrastructure for database, authentication, and real-time features.

## Installation

### Supabase CLI

The Supabase CLI is installed in the `ZEN/TOOLS/` directory:
- **Binary**: `ZEN/TOOLS/supabase.exe`
- **Version**: 2.40.7
- **License**: Located in `DOCS/Supabase_LICENSE`

### Project Structure

```
ZEN/
├── TOOLS/
│   └── supabase.exe          # Supabase CLI tool
└── supabase/
    ├── .gitignore           # Supabase-specific gitignore
    ├── config.toml          # Main configuration file
    └── .temp/               # Temporary files (ignored by git)
        └── cli-latest
```

## Configuration

### Project Settings

- **Project ID**: `ZEN`
- **API Port**: 54321
- **Database Port**: 54322
- **Studio Port**: 54323 (Supabase Admin UI)
- **Database Version**: PostgreSQL 17

### Key Features Enabled

- **API**: REST and GraphQL endpoints
- **Database**: PostgreSQL with migrations support
- **Authentication**: Email/password, OAuth providers
- **Realtime**: Real-time subscriptions
- **Storage**: File storage with 50MiB limit
- **Studio**: Web-based database management
- **Edge Functions**: Deno runtime for serverless functions

### Environment Variables

The following environment variables can be configured:
- `OPENAI_API_KEY`: For Supabase AI features in Studio
- `SUPABASE_AUTH_SMS_TWILIO_AUTH_TOKEN`: For SMS authentication
- OAuth provider secrets (Apple, Google, etc.)

## Usage

### Basic Commands

From the `ZEN` directory:

```bash
# Start local Supabase development stack
./TOOLS/supabase.exe start

# Stop local development stack
./TOOLS/supabase.exe stop

# View status of local services
./TOOLS/supabase.exe status

# Access Supabase Studio
# Navigate to http://localhost:54323
```

### Development Workflow

1. **Local Development**: Use `supabase start` to run the full stack locally
2. **Database Migrations**: Store SQL migrations in `supabase/migrations/`
3. **Seed Data**: Place seed files in `supabase/seed.sql`
4. **Edge Functions**: Store Deno functions in `supabase/functions/`

## Integration with .NET

The Supabase configuration is designed to work with the existing .NET infrastructure:

- **Connection String**: Available when local stack is running
- **Typed Options**: Already configured in `Infrastructure/Data/SupabaseOptions.cs`
- **DI Registration**: Pre-configured in the API project

## Security Considerations

- All secrets use environment variable substitution (`env(VARIABLE_NAME)`)
- Local development uses test credentials only
- Production configuration requires separate environment setup
- `.temp` directory is automatically ignored by Git

## Next Steps

1. **Start Local Stack**: Run `./TOOLS/supabase.exe start` to begin development
2. **Create Migrations**: Add database schema migrations
3. **Configure Authentication**: Set up OAuth providers as needed
4. **Deploy**: Link to production Supabase project for deployment

---

*Setup completed: 2025-09-13*
*Project conceived and specified by Ram Revanur*