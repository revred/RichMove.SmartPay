using System.Diagnostics.Metrics;

namespace SmartPay.Api.Analytics;

public static class MetricsRegistry
{
    public const string MeterName = "SmartPay.Api";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> RequestsTotal =
        Meter.CreateCounter<long>("http.requests.total", "requests", "Total HTTP requests by route and status");

    public static readonly Histogram<double> RequestDurationMs =
        Meter.CreateHistogram<double>("http.request.duration.ms", "ms", "Request duration in milliseconds");
}