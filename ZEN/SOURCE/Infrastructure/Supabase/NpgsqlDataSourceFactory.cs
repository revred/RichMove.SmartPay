using Microsoft.Extensions.Options;
using Npgsql;
using RichMove.SmartPay.Infrastructure.Data;

namespace RichMove.SmartPay.Infrastructure.Supabase;

/// <summary>
/// Provides a pooled NpgsqlDataSource for Supabase Postgres.
/// Made resilient to differing shapes of SupabaseOptions by using reflection + env fallbacks.
/// </summary>
public static class NpgsqlDataSourceFactory
{
    public static NpgsqlDataSource Create(IOptions<SupabaseOptions> options)
    {
        var cfg = options.Value;

        // Enabled flag: try property, else env var, default false
        var enabled = GetBool(cfg, "Enabled")
                      ?? GetBool(cfg, "UseSupabase")
                      ?? TryParseBoolEnv("Supabase__Enabled")
                      ?? false;
        if (!enabled)
            throw new InvalidOperationException("Supabase is disabled.");

        // Connection string: try multiple sources
        var conn = GetString(cfg, "DbConnectionString")
                   ?? GetString(cfg, "DatabaseConnectionString")
                   ?? GetString(cfg, "ConnectionString")
                   ?? Environment.GetEnvironmentVariable("Supabase__DbConnectionString")
                   ?? Environment.GetEnvironmentVariable("SUPABASE_DB_CONNECTION_STRING")
                   ?? Environment.GetEnvironmentVariable("ConnectionStrings__Supabase");

        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("Supabase connection string not configured. Set Supabase__DbConnectionString or ensure SupabaseOptions contains it.");

        var builder = new NpgsqlDataSourceBuilder(conn);
        // Optional: tuning here if needed
        return builder.Build();
    }

    private static string? GetString(object obj, string name)
        => obj.GetType().GetProperty(name)?.GetValue(obj) as string;

    private static bool? GetBool(object obj, string name)
    {
        var p = obj.GetType().GetProperty(name);
        if (p is null) return null;
        var v = p.GetValue(obj);
        return v switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var b) => b,
            _ => null
        };
    }

    private static bool? TryParseBoolEnv(string name)
    {
        var s = Environment.GetEnvironmentVariable(name);
        return bool.TryParse(s, out var b) ? b : null;
    }
}