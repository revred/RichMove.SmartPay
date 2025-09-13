using FastEndpoints;
using Microsoft.Extensions.Options;
using Npgsql;
using RichMove.SmartPay.Infrastructure.Data;
using RichMove.SmartPay.Infrastructure.Supabase;

namespace RichMove.SmartPay.Api.Endpoints.Health;

/// <summary>
/// Performs a lightweight DB connectivity check when Supabase is enabled.
/// GET /v1/health/db
/// </summary>
internal sealed class DbEndpoint : EndpointWithoutRequest<DbHealthResponse>
{
    private readonly IOptions<SupabaseOptions> _supa;
    private readonly IServiceProvider _sp;

    public DbEndpoint(IOptions<SupabaseOptions> supa, IServiceProvider sp)
    {
        _supa = supa;
        _sp = sp;
    }

    public override void Configure()
    {
        Get("/v1/health/db");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Database connectivity health";
            s.Description = "Checks basic DB connectivity/status when Supabase is enabled.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var enabled = _supa.Value.Enabled;
        var resp = new DbHealthResponse { SupabaseEnabled = enabled };

        if (!enabled)
        {
            await SendOkAsync(resp, ct).ConfigureAwait(false);
            return;
        }

        var ds = _sp.GetService<NpgsqlDataSource>();
        if (ds is null)
        {
            resp.Error = "NpgsqlDataSource not configured";
            await SendOkAsync(resp, ct).ConfigureAwait(false);
            return;
        }

        var started = DateTime.UtcNow;
        try
        {
            await using var cmd = ds.CreateCommand("select 1");
            var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            resp.Connected = (result is int i && i == 1) || $"{result}" == "1";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            resp.Error = ex.GetType().Name + ": " + ex.Message;
            resp.Connected = false;
        }
        finally
        {
            resp.DurationMs = (int)(DateTime.UtcNow - started).TotalMilliseconds;
        }

        await SendOkAsync(resp, ct).ConfigureAwait(false);
    }
}

internal sealed class DbHealthResponse
{
    public bool SupabaseEnabled { get; init; }
    public bool Connected { get; set; }
    public string? Error { get; set; }
    public int? DurationMs { get; set; }
}