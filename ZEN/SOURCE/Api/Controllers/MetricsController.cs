using Microsoft.AspNetCore.Mvc;
using RichMove.SmartPay.Api.Monitoring;
using RichMove.SmartPay.Api.Infrastructure.Deployment;
using System.Globalization;

namespace RichMove.SmartPay.Api.Controllers;

[ApiController]
[Route("metrics")]
public sealed class MetricsController : ControllerBase
{
    private readonly PrometheusMetricsService _metricsService;
    private readonly KubernetesDeploymentService _deploymentService;

    public MetricsController(
        PrometheusMetricsService metricsService,
        KubernetesDeploymentService deploymentService)
    {
        _metricsService = metricsService;
        _deploymentService = deploymentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMetrics()
    {
        var metrics = await _metricsService.CollectAllMetricsAsync();

        // Add deployment context to metrics
        var deploymentInfo = await _deploymentService.GetDeploymentInfoAsync();
        metrics["deployment"] = deploymentInfo;

        return Ok(metrics);
    }

    [HttpGet("prometheus")]
    public async Task<IActionResult> GetPrometheusMetrics()
    {
        var metrics = await _metricsService.CollectAllMetricsAsync();
        var prometheusFormat = ConvertToPrometheusFormat(metrics);

        return Content(prometheusFormat, "text/plain; version=0.0.4");
    }

    [HttpGet("deployment")]
    public async Task<IActionResult> GetDeploymentInfo()
    {
        var info = await _deploymentService.GetDeploymentInfoAsync();
        return Ok(info);
    }

    private static string ConvertToPrometheusFormat(Dictionary<string, object> metrics)
    {
        var output = new System.Text.StringBuilder();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var metric in metrics)
        {
            if (metric.Key == "deployment" || metric.Key == "timestamp")
                continue;

            var metricName = metric.Key.Replace(".", "_", StringComparison.Ordinal).Replace("-", "_", StringComparison.Ordinal);
            var value = metric.Value?.ToString() ?? "0";

            output.AppendLine(CultureInfo.InvariantCulture, $"# TYPE {metricName} gauge");
            output.AppendLine(CultureInfo.InvariantCulture, $"{metricName} {value} {timestamp}");
        }

        return output.ToString();
    }
}