using Microsoft.AspNetCore.Mvc;
using RichMove.SmartPay.Api.Security;
using RichMove.SmartPay.Api.Compliance;

namespace RichMove.SmartPay.Api.Controllers;

[ApiController]
[Route("security")]
public sealed class SecurityController : ControllerBase
{
    private readonly SecurityScanningService _securityScanning;
    private readonly ThreatDetectionService _threatDetection;
    private readonly ComplianceMonitoringService _complianceMonitoring;

    public SecurityController(
        SecurityScanningService securityScanning,
        ThreatDetectionService threatDetection,
        ComplianceMonitoringService complianceMonitoring)
    {
        _securityScanning = securityScanning;
        _threatDetection = threatDetection;
        _complianceMonitoring = complianceMonitoring;
    }

    [HttpGet("dashboard")]
    public IActionResult GetSecurityDashboard()
    {
        var scanStatus = _securityScanning.GetScanStatus();
        var threatStatus = _threatDetection.GetThreatStatus();
        var complianceReport = _complianceMonitoring.GetComplianceReport();

        var dashboard = new SecurityDashboard
        {
            GeneratedAt = DateTime.UtcNow,
            SecurityScanning = new SecurityScanDashboard
            {
                LastScanTime = scanStatus.LastScanTime,
                IsRunning = scanStatus.IsRunning,
                TotalVulnerabilities = scanStatus.TotalVulnerabilities,
                NextScanTime = scanStatus.NextScanTime,
                ScanResults = scanStatus.ScanResults.Select(r => new ScanResultSummary
                {
                    ScanType = r.ScanType,
                    Status = r.Status.ToString(),
                    Findings = r.Findings,
                    Duration = r.EndTime - r.StartTime
                }).ToList()
            },
            ThreatDetection = new ThreatDetectionDashboard
            {
                LastAnalysis = threatStatus.LastAnalysis,
                CurrentThreatLevel = threatStatus.CurrentThreatLevel,
                TotalThreatsDetected = threatStatus.TotalThreatsDetected,
                ActiveClientProfiles = threatStatus.ActiveClientProfiles,
                QueuedEvents = threatStatus.QueuedEvents,
                ThreatsByType = threatStatus.ThreatCountsByType.Select(kvp => new ThreatTypeSummary
                {
                    Type = kvp.Key.ToString(),
                    Count = kvp.Value
                }).ToList()
            },
            ComplianceMonitoring = new ComplianceDashboard
            {
                GeneratedAt = complianceReport.GeneratedAt,
                ComplianceScore = complianceReport.ComplianceScore,
                TotalViolations = complianceReport.TotalViolations,
                CriticalViolations = complianceReport.CriticalViolations,
                FrameworkStatus = complianceReport.FrameworkStatus.Select(fs => new FrameworkStatusSummary
                {
                    Framework = fs.Framework.ToString(),
                    Status = fs.Status.ToString(),
                    LastChecked = fs.LastChecked,
                    ViolationCount = fs.Violations.Count
                }).ToList()
            }
        };

        return Ok(dashboard);
    }

    [HttpGet("threats")]
    public IActionResult GetThreatStatus()
    {
        var status = _threatDetection.GetThreatStatus();
        return Ok(status);
    }

    [HttpPost("threats/event")]
    public IActionResult RecordSecurityEvent([FromBody] SecurityEventRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _threatDetection.RecordSecurityEvent(
            request.Source,
            request.ClientId,
            request.Data,
            request.Severity);

        return Ok(new { message = "Security event recorded successfully" });
    }

    [HttpGet("scans")]
    public IActionResult GetSecurityScans()
    {
        var scanStatus = _securityScanning.GetScanStatus();
        return Ok(scanStatus);
    }

    [HttpGet("compliance")]
    public IActionResult GetComplianceReport()
    {
        var report = _complianceMonitoring.GetComplianceReport();
        return Ok(report);
    }

    [HttpPost("compliance/event")]
    public IActionResult RecordComplianceEvent([FromBody] ComplianceEventRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _complianceMonitoring.RecordComplianceEvent(
            request.EventType,
            request.Framework,
            request.Severity,
            request.Details,
            request.UserId);

        return Ok(new { message = "Compliance event recorded successfully" });
    }
}

// Dashboard data models
public sealed class SecurityDashboard
{
    public DateTime GeneratedAt { get; set; }
    public SecurityScanDashboard SecurityScanning { get; set; } = new();
    public ThreatDetectionDashboard ThreatDetection { get; set; } = new();
    public ComplianceDashboard ComplianceMonitoring { get; set; } = new();
}

public sealed class SecurityScanDashboard
{
    public DateTime LastScanTime { get; set; }
    public DateTime NextScanTime { get; set; }
    public bool IsRunning { get; set; }
    public int TotalVulnerabilities { get; set; }
    public List<ScanResultSummary> ScanResults { get; set; } = [];
}

public sealed class ScanResultSummary
{
    public string ScanType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Findings { get; set; }
    public TimeSpan Duration { get; set; }
}

public sealed class ThreatDetectionDashboard
{
    public DateTime LastAnalysis { get; set; }
    public long CurrentThreatLevel { get; set; }
    public int TotalThreatsDetected { get; set; }
    public int ActiveClientProfiles { get; set; }
    public int QueuedEvents { get; set; }
    public List<ThreatTypeSummary> ThreatsByType { get; set; } = [];
}

public sealed class ThreatTypeSummary
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class ComplianceDashboard
{
    public DateTime GeneratedAt { get; set; }
    public double ComplianceScore { get; set; }
    public int TotalViolations { get; set; }
    public int CriticalViolations { get; set; }
    public List<FrameworkStatusSummary> FrameworkStatus { get; set; } = [];
}

public sealed class FrameworkStatusSummary
{
    public string Framework { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime LastChecked { get; set; }
    public int ViolationCount { get; set; }
}

// Request models
public sealed class SecurityEventRequest
{
    public string Source { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public SecurityEventSeverity Severity { get; set; } = SecurityEventSeverity.Low;
}

public sealed class ComplianceEventRequest
{
    public string EventType { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string? UserId { get; set; }
}