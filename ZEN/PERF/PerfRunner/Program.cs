using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

// Simple self-contained perf runner for SmartPay API.
// Drives a fixed RPS over a duration and computes latency distribution.

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var cfg = PerfConfig.FromEnv();
        Console.WriteLine($"[PerfRunner] BaseUrl={cfg.BaseUrl}, RPS={cfg.Rps}, Duration={cfg.DurationSeconds}s, Warmup={cfg.WarmupSeconds}s");

        Console.WriteLine("[PerfRunner] Build mode expectation: Release");

        using var http = new HttpClient
        {
            BaseAddress = new Uri(cfg.BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };

        http.DefaultRequestHeaders.Accept.Clear();
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Warmup phase
        if (cfg.WarmupSeconds > 0)
        {
            Console.WriteLine($"[PerfRunner] Warmup phase: {cfg.WarmupSeconds}s at 10 RPS");
            await RunLoadPhase(http, cfg.WarmupSeconds, 10, discard: true);
        }

        // Main measurement phase
        Console.WriteLine($"[PerfRunner] Measurement phase: {cfg.DurationSeconds}s at {cfg.Rps} RPS");
        var metrics = await RunLoadPhase(http, cfg.DurationSeconds, cfg.Rps, discard: false);

        Console.WriteLine("[PerfRunner] Phase complete.");

        // Gate evaluation
        var gate = PerfGate.LoadYaml();
        var best = BestKnown.LoadYaml();
        var gateResult = GateEvaluator.Evaluate(gate, best, metrics);

        // Persist results
        var outputDir = Path.Combine(AppContext.BaseDirectory, "perf-results");
        Directory.CreateDirectory(outputDir);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

        var jsonPath = Path.Combine(outputDir, $"perf-{timestamp}.json");
        var reportPath = Path.Combine(outputDir, $"report-{timestamp}.md");

        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(metrics, JsonOpts.Options));
        await File.WriteAllTextAsync(reportPath, ReportGenerator.Generate(metrics, gateResult));

        Console.WriteLine($"[PerfRunner] Results written to {jsonPath} and {reportPath}");

        return gateResult.Passed ? 0 : 1;
    }

    private static async Task<Metrics> RunLoadPhase(HttpClient http, int durationSeconds, int targetRps, bool discard)
    {
        var results = new List<RequestResult>();
        var sw = Stopwatch.StartNew();
        var endTime = TimeSpan.FromSeconds(durationSeconds);
        var intervalMs = 1000.0 / targetRps;
        var nextRequestTime = sw.Elapsed;

        var requestCount = 0;
        var errorCount = 0;

        while (sw.Elapsed < endTime)
        {
            if (sw.Elapsed >= nextRequestTime)
            {
                var result = await MakeRequest(http);
                requestCount++;

                if (!discard)
                    results.Add(result);

                if (!result.Success)
                    errorCount++;

                nextRequestTime = nextRequestTime.Add(TimeSpan.FromMilliseconds(intervalMs));

                if (requestCount % 100 == 0)
                    Console.WriteLine($"[PerfRunner] Sent {requestCount} requests, {errorCount} errors");
            }

            await Task.Delay(1);
        }

        if (discard)
            return new Metrics();

        return ComputeMetrics(results, sw.Elapsed.TotalSeconds);
    }

    private static async Task<RequestResult> MakeRequest(HttpClient http)
    {
        var requestSw = Stopwatch.StartNew();
        var payload = JsonSerializer.Serialize(new
        {
            from_currency = "USD",
            to_currency = "EUR",
            amount = 2500.0m
        }, JsonOpts.Options);

        try
        {
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await http.PostAsync("/fx/quote", content);

            requestSw.Stop();

            var responseBody = await response.Content.ReadAsStringAsync();
            var success = response.IsSuccessStatusCode;

            return new RequestResult
            {
                Success = success,
                LatencyMs = requestSw.Elapsed.TotalMilliseconds,
                StatusCode = (int)response.StatusCode,
                ResponseSize = responseBody.Length
            };
        }
        catch (Exception ex)
        {
            requestSw.Stop();
            return new RequestResult
            {
                Success = false,
                LatencyMs = requestSw.Elapsed.TotalMilliseconds,
                StatusCode = 0,
                Error = ex.Message
            };
        }
    }

    private static Metrics ComputeMetrics(List<RequestResult> results, double durationSeconds)
    {
        var successResults = results.Where(r => r.Success).ToList();
        var latencies = successResults.Select(r => r.LatencyMs).OrderBy(x => x).ToList();

        var metrics = new Metrics
        {
            Endpoint = "fx_quote",
            RequestCount = results.Count,
            SuccessCount = successResults.Count,
            ErrorCount = results.Count - successResults.Count,
            ErrorRate_Pct = results.Count > 0 ? (results.Count - successResults.Count) * 100.0 / results.Count : 0,
            DurationSeconds = durationSeconds
        };

        if (latencies.Count > 0)
        {
            metrics.P50_ms = Percentile(latencies, 0.50);
            metrics.P90_ms = Percentile(latencies, 0.90);
            metrics.P95_ms = Percentile(latencies, 0.95);
            metrics.P99_ms = Percentile(latencies, 0.99);
            metrics.Min_ms = latencies.Min();
            metrics.Max_ms = latencies.Max();
            metrics.Avg_ms = latencies.Average();
        }

        return metrics;
    }

    private static double Percentile(List<double> sortedList, double percentile)
    {
        if (sortedList.Count == 0) return 0;
        var index = percentile * (sortedList.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper) return sortedList[lower];
        return sortedList[lower] * (upper - index) + sortedList[upper] * (index - lower);
    }
}

internal sealed class PerfConfig
{
    public string BaseUrl { get; set; } = "http://localhost:5000";
    public int Rps { get; set; } = 50;
    public int DurationSeconds { get; set; } = 30;
    public int WarmupSeconds { get; set; } = 5;

    public static PerfConfig FromEnv() => new()
    {
        BaseUrl = Environment.GetEnvironmentVariable("SMARTPAY_BASE_URL") ?? "http://localhost:5000",
        Rps = int.Parse(Environment.GetEnvironmentVariable("SMARTPAY_RPS") ?? "50"),
        DurationSeconds = int.Parse(Environment.GetEnvironmentVariable("SMARTPAY_DURATION_SECONDS") ?? "30"),
        WarmupSeconds = int.Parse(Environment.GetEnvironmentVariable("SMARTPAY_WARMUP_SECONDS") ?? "5")
    };
}

internal sealed class RequestResult
{
    public bool Success { get; set; }
    public double LatencyMs { get; set; }
    public int StatusCode { get; set; }
    public int ResponseSize { get; set; }
    public string? Error { get; set; }
}

internal sealed class Metrics
{
    public string Endpoint { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public double ErrorRate_Pct { get; set; }
    public double P50_ms { get; set; }
    public double P90_ms { get; set; }
    public double P95_ms { get; set; }
    public double P99_ms { get; set; }
    public double Min_ms { get; set; }
    public double Max_ms { get; set; }
    public double Avg_ms { get; set; }
    public double DurationSeconds { get; set; }
}

internal sealed class PerfGate
{
    public Dictionary<string, EndpointGate> Targets { get; set; } = new();

    public static PerfGate LoadYaml()
    {
        var path = FindRepoFile(Path.Combine("ANALYSIS", "PERF", "PerfGate.yaml"));
        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<PerfGate>(yaml)!;
    }

    public static string FindRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, relativePath)))
            dir = dir.Parent;

        if (dir == null)
            throw new FileNotFoundException($"Could not find {relativePath} in any parent directory");

        return Path.Combine(dir.FullName, relativePath);
    }

    public EndpointGate? Get(string endpoint) => Targets.TryGetValue(endpoint, out var v) ? v : null;
}

internal sealed class EndpointGate
{
    public AbsoluteThresholds Absolute { get; set; } = new();
    public RegressionBudget RegressionBudgetPct { get; set; } = new();
}

internal sealed class AbsoluteThresholds
{
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public double ErrorRatePct { get; set; }
}

internal sealed class RegressionBudget
{
    public double P95 { get; set; }
    public double P99 { get; set; }
}

internal sealed class BestKnown
{
    // YAML layout is a flat map of endpoint -> values
    public Dictionary<string, BestEndpoint> Endpoints { get; set; } = new();

    public static BestKnown? TryLoadYaml()
    {
        try
        {
            var path = PerfGate.FindRepoFile(Path.Combine("ANALYSIS", "PERF", "Baselines", "BestKnown.yaml"));
            var yaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            var map = deserializer.Deserialize<Dictionary<string, BestEndpoint>>(yaml) ?? new();
            return new BestKnown { Endpoints = map };
        }
        catch { return null; }
    }

    public static BestKnown LoadYaml() => TryLoadYaml() ?? new BestKnown();

    public BestEndpoint? Get(string endpoint) => Endpoints.TryGetValue(endpoint, out var v) ? v : null;
}

internal sealed class BestEndpoint
{
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public string? Source { get; set; }
    public DateTime Timestamp { get; set; }
}

internal static class JsonOpts
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

internal static class GateEvaluator
{
    public static GateResult Evaluate(PerfGate gate, BestKnown bestKnown, Metrics metrics)
    {
        var result = new GateResult { Passed = true };

        var endpointGate = gate.Get(metrics.Endpoint);
        if (endpointGate == null)
        {
            result.Messages.Add($"No gate defined for endpoint '{metrics.Endpoint}' - treating as pass");
            return result;
        }

        // Absolute thresholds
        var abs = endpointGate.Absolute;
        if (metrics.P95_ms > abs.P95Ms)
        {
            result.Passed = false;
            result.Messages.Add($"FAIL: P95 {metrics.P95_ms:F1}ms > {abs.P95Ms}ms threshold");
        }
        if (metrics.P99_ms > abs.P99Ms)
        {
            result.Passed = false;
            result.Messages.Add($"FAIL: P99 {metrics.P99_ms:F1}ms > {abs.P99Ms}ms threshold");
        }
        if (metrics.ErrorRate_Pct > abs.ErrorRatePct)
        {
            result.Passed = false;
            result.Messages.Add($"FAIL: Error rate {metrics.ErrorRate_Pct:F2}% > {abs.ErrorRatePct}% threshold");
        }

        // Regression (if baseline exists)
        var best = BestKnown.TryLoadYaml()?.Get(metrics.Endpoint);
        if (best is not null)
        {
            var p95Delta = PercentIncrease(best.P95Ms, metrics.P95_ms);
            var p99Delta = PercentIncrease(best.P99Ms, metrics.P99_ms);
            if (p95Delta > endpointGate.RegressionBudgetPct.P95 || p99Delta > endpointGate.RegressionBudgetPct.P99)
            {
                result.Passed = false;
                result.Messages.Add($"FAIL: Regression P95={p95Delta:F1}% P99={p99Delta:F1}% vs budget P95={endpointGate.RegressionBudgetPct.P95}% P99={endpointGate.RegressionBudgetPct.P99}%");
            }
        }

        if (result.Passed)
            result.Messages.Add("PASS: All gates passed");

        return result;
    }

    private static double PercentIncrease(double baseline, double current) =>
        baseline > 0 ? ((current - baseline) / baseline) * 100.0 : 0.0;
}

internal sealed class GateResult
{
    public bool Passed { get; set; }
    public List<string> Messages { get; set; } = new();
}

internal static class ReportGenerator
{
    public static string Generate(Metrics metrics, GateResult gateResult)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Performance Test Report");
        sb.AppendLine();
        sb.AppendLine($"**Endpoint:** `{metrics.Endpoint}`");
        sb.AppendLine($"**Duration:** {metrics.DurationSeconds:F1}s");
        sb.AppendLine($"**Requests:** {metrics.RequestCount} ({metrics.SuccessCount} success, {metrics.ErrorCount} errors)");
        sb.AppendLine($"**Error Rate:** {metrics.ErrorRate_Pct:F2}%");
        sb.AppendLine();
        sb.AppendLine("## Latency Distribution");
        sb.AppendLine();
        sb.AppendLine($"| Percentile | Latency (ms) |");
        sb.AppendLine($"|------------|--------------|");
        sb.AppendLine($"| P50        | {metrics.P50_ms:F1}      |");
        sb.AppendLine($"| P90        | {metrics.P90_ms:F1}      |");
        sb.AppendLine($"| P95        | {metrics.P95_ms:F1}      |");
        sb.AppendLine($"| P99        | {metrics.P99_ms:F1}      |");
        sb.AppendLine($"| Min        | {metrics.Min_ms:F1}      |");
        sb.AppendLine($"| Max        | {metrics.Max_ms:F1}      |");
        sb.AppendLine($"| Avg        | {metrics.Avg_ms:F1}      |");
        sb.AppendLine();
        sb.AppendLine("## Gate Results");
        sb.AppendLine();
        sb.AppendLine($"**Status:** {(gateResult.Passed ? "✅ PASSED" : "❌ FAILED")}");
        sb.AppendLine();
        foreach (var msg in gateResult.Messages)
        {
            sb.AppendLine($"- {msg}");
        }
        return sb.ToString();
    }
}