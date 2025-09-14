using Microsoft.AspNetCore.Mvc;
using RichMove.SmartPay.Api.Infrastructure.Scalability;

namespace RichMove.SmartPay.Api.Controllers;

[ApiController]
[Route("scaling")]
public sealed class ScalingController : ControllerBase
{
    private readonly AutoScalingService _autoScalingService;

    public ScalingController(AutoScalingService autoScalingService)
    {
        _autoScalingService = autoScalingService;
    }

    [HttpGet("status")]
    public IActionResult GetScalingStatus()
    {
        var status = _autoScalingService.GetScalingStatus();
        return Ok(status);
    }

    [HttpPost("load")]
    public IActionResult RecordLoadMetrics([FromBody] LoadMetricsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _autoScalingService.RecordLoadMetrics(
            request.Endpoint,
            request.RequestCount,
            request.ResponseTime);

        return Ok(new { message = "Load metrics recorded successfully" });
    }
}

public sealed class LoadMetricsRequest
{
    public string Endpoint { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public double ResponseTime { get; set; }
}