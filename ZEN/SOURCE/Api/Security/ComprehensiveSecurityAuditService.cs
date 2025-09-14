using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RichMove.SmartPay.Core.Observability;
using RichMove.SmartPay.Core.Compliance;

namespace RichMove.SmartPay.Api.Security;

public sealed class ComprehensiveSecurityAuditService : IHostedService, IDisposable
{
    private readonly ILogger<ComprehensiveSecurityAuditService> _logger;
    private readonly SecurityAuditOptions _options;
    private readonly Timer _auditTimer;
    private readonly Timer _reportTimer;
    private readonly ConcurrentQueue<ComprehensiveAuditEvent> _auditQueue;
    private readonly ConcurrentDictionary<string, SecurityMetrics> _securityMetrics;
    private readonly Counter<long> _auditEventCount;
    private readonly Counter<long> _securityViolationCount;
    private readonly Histogram<double> _auditProcessingTime;
    private readonly List<SecurityAuditRule> _auditRules;

    private static readonly Dictionary<AuditEventType, AuditSeverity> DefaultSeverityMapping = new()
    {
        [AuditEventType.Authentication] = AuditSeverity.Medium,
        [AuditEventType.Authorization] = AuditSeverity.High,
        [AuditEventType.DataAccess] = AuditSeverity.Medium,
        [AuditEventType.ConfigurationChange] = AuditSeverity.High,
        [AuditEventType.SecurityViolation] = AuditSeverity.Critical,
        [AuditEventType.SystemAccess] = AuditSeverity.Medium,
        [AuditEventType.PrivilegeEscalation] = AuditSeverity.Critical,
        [AuditEventType.DataModification] = AuditSeverity.High,
        [AuditEventType.ApiAccess] = AuditSeverity.Low,
        [AuditEventType.FileAccess] = AuditSeverity.Medium
    };

    public ComprehensiveSecurityAuditService(
        ILogger<ComprehensiveSecurityAuditService> logger,
        IOptions<SecurityAuditOptions> options,
        IMeterFactory meterFactory)
    {
        _logger = logger;
        _options = options.Value;
        _auditQueue = new ConcurrentQueue<ComprehensiveAuditEvent>();
        _securityMetrics = new ConcurrentDictionary<string, SecurityMetrics>();
        _auditRules = new List<SecurityAuditRule>();

        var meter = meterFactory.Create("RichMove.SmartPay.SecurityAudit");
        _auditEventCount = meter.CreateCounter<long>("richmove_smartpay_audit_events_total");
        _securityViolationCount = meter.CreateCounter<long>("richmove_smartpay_security_violations_total");
        _auditProcessingTime = meter.CreateHistogram<double>("richmove_smartpay_audit_processing_duration_seconds");

        _auditTimer = new Timer(ProcessAuditEvents, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        _reportTimer = new Timer(GenerateSecurityReport, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

        InitializeAuditRules();

        _logger.LogInformation("Comprehensive Security Audit Service initialized with {RuleCount} audit rules", _auditRules.Count);
    }

    public async Task LogSecurityEventAsync(ComprehensiveAuditEvent auditEvent)
    {
        if (auditEvent == null) return;

        auditEvent.Timestamp = DateTime.UtcNow;
        auditEvent.AuditId = GenerateAuditId();

        // Apply audit rules
        foreach (var rule in _auditRules.Where(r => r.IsEnabled))
        {
            if (await rule.MatchesAsync(auditEvent))
            {
                auditEvent.TriggeredRules.Add(rule.Name);
                auditEvent.Severity = (AuditSeverity)Math.Max((int)auditEvent.Severity, (int)rule.Severity);

                if (rule.AutoEscalate)
                {
                    EscalateSecurityIncidentAsync(auditEvent, rule);
                }
            }
        }

        _auditQueue.Enqueue(auditEvent);

        _auditEventCount.Add(1,
            new KeyValuePair<string, object?>("event_type", auditEvent.EventType.ToString()),
            new KeyValuePair<string, object?>("severity", auditEvent.Severity.ToString()),
            new KeyValuePair<string, object?>("user", auditEvent.UserId ?? "anonymous"));

        if (auditEvent.Severity >= AuditSeverity.High)
        {
            _securityViolationCount.Add(1,
                new KeyValuePair<string, object?>("event_type", auditEvent.EventType.ToString()),
                new KeyValuePair<string, object?>("source", auditEvent.Source));

            _logger.LogWarning("High-severity security event: {EventType} from {Source} by user {User}",
                auditEvent.EventType, auditEvent.Source, auditEvent.UserId ?? "anonymous");
        }

        // Immediate processing for critical events
        if (auditEvent.Severity == AuditSeverity.Critical)
        {
            ProcessCriticalEventAsync(auditEvent);
        }
    }

    public SecurityAuditReport GenerateAuditReportAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-7);
        var end = endDate ?? DateTime.UtcNow;

        var report = new SecurityAuditReport
        {
            ReportId = Guid.NewGuid().ToString(),
            GeneratedAt = DateTime.UtcNow,
            PeriodStart = start,
            PeriodEnd = end
        };

        // Get events from the audit queue and any persistent storage
        var events = GetAuditEventsForPeriod(start, end);

        report.TotalEvents = events.Count;
        report.EventsByType = events.GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());
        report.EventsBySeverity = events.GroupBy(e => e.Severity)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        // Security violations analysis
        var violations = events.Where(e => e.Severity >= AuditSeverity.High).ToList();
        report.SecurityViolations = violations.Count;
        report.CriticalIncidents = violations.Count(e => e.Severity == AuditSeverity.Critical);

        // User activity analysis
        report.UserActivitySummary = events
            .Where(e => !string.IsNullOrEmpty(e.UserId))
            .GroupBy(e => e.UserId)
            .ToDictionary(g => g.Key!, g => new UserActivitySummary
            {
                UserId = g.Key!,
                EventCount = g.Count(),
                LastActivity = g.Max(e => e.Timestamp),
                ViolationCount = g.Count(e => e.Severity >= AuditSeverity.High)
            });

        // System access patterns
        report.AccessPatterns = AnalyzeAccessPatterns(events);

        // Risk assessment
        report.RiskAssessment = PerformRiskAssessmentAsync(events);

        // Recommendations
        report.SecurityRecommendations = GenerateSecurityRecommendations(events, report.RiskAssessment);

        _logger.LogInformation("Generated security audit report {ReportId} covering {EventCount} events",
            report.ReportId, report.TotalEvents);

        return report;
    }

    public List<SecurityAnomalyDetection> DetectAnomaliesAsync()
    {
        var anomalies = new List<SecurityAnomalyDetection>();
        var recentEvents = GetRecentAuditEvents(TimeSpan.FromHours(24));

        // Unusual login patterns
        var loginAnomalies = DetectLoginAnomalies(recentEvents);
        anomalies.AddRange(loginAnomalies);

        // Privilege escalation attempts
        var privilegeAnomalies = DetectPrivilegeAnomalies(recentEvents);
        anomalies.AddRange(privilegeAnomalies);

        // Data access anomalies
        var dataAccessAnomalies = DetectDataAccessAnomalies(recentEvents);
        anomalies.AddRange(dataAccessAnomalies);

        // Time-based anomalies
        var timeAnomalies = DetectTimeBasedAnomalies(recentEvents);
        anomalies.AddRange(timeAnomalies);

        // Geographic anomalies
        var geoAnomalies = DetectGeographicAnomalies(recentEvents);
        anomalies.AddRange(geoAnomalies);

        foreach (var anomaly in anomalies.Where(a => a.Severity >= AuditSeverity.High))
        {
            _logger.LogWarning("Security anomaly detected: {Type} - {Description}",
                anomaly.AnomalyType, anomaly.Description);
        }

        return anomalies;
    }

    public ComplianceReport GenerateComplianceReportAsync(ComplianceFramework framework)
    {
        var report = new ComplianceReport
        {
            Framework = framework,
            GeneratedAt = DateTime.UtcNow,
            AssessmentPeriod = TimeSpan.FromDays(30)
        };

        var events = GetAuditEventsForPeriod(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        switch (framework)
        {
            case ComplianceFramework.GDPR:
                report = GenerateGDPRComplianceReport(events, report);
                break;
            case ComplianceFramework.PCIDSS:
                report = GeneratePCIComplianceReport(events, report);
                break;
            case ComplianceFramework.SOX:
                report = GenerateSOXComplianceReport(events, report);
                break;
            case ComplianceFramework.HIPAA:
                report = GenerateHIPAAComplianceReport(events, report);
                break;
        }

        return report;
    }

    private void InitializeAuditRules()
    {
        _auditRules.AddRange(new[]
        {
            new SecurityAuditRule
            {
                Name = "Multiple Failed Login Attempts",
                EventType = AuditEventType.Authentication,
                Severity = AuditSeverity.High,
                IsEnabled = true,
                AutoEscalate = true,
                MatchCondition = (e) => Task.FromResult(e.EventType == AuditEventType.Authentication &&
                                             e.Details.Contains("failed", StringComparison.OrdinalIgnoreCase))
            },

            new SecurityAuditRule
            {
                Name = "Privilege Escalation Attempt",
                EventType = AuditEventType.PrivilegeEscalation,
                Severity = AuditSeverity.Critical,
                IsEnabled = true,
                AutoEscalate = true,
                MatchCondition = (e) => Task.FromResult(e.EventType == AuditEventType.PrivilegeEscalation)
            },

            new SecurityAuditRule
            {
                Name = "Administrative Configuration Change",
                EventType = AuditEventType.ConfigurationChange,
                Severity = AuditSeverity.High,
                IsEnabled = true,
                AutoEscalate = false,
                MatchCondition = (e) => Task.FromResult(e.EventType == AuditEventType.ConfigurationChange &&
                                             e.Details.Contains("admin", StringComparison.OrdinalIgnoreCase))
            },

            new SecurityAuditRule
            {
                Name = "Sensitive Data Access",
                EventType = AuditEventType.DataAccess,
                Severity = AuditSeverity.Medium,
                IsEnabled = true,
                AutoEscalate = false,
                MatchCondition = (e) => Task.FromResult(e.EventType == AuditEventType.DataAccess &&
                                             (e.Resource.Contains("payment", StringComparison.OrdinalIgnoreCase) ||
                                              e.Resource.Contains("personal", StringComparison.OrdinalIgnoreCase)))
            },

            new SecurityAuditRule
            {
                Name = "Off-Hours System Access",
                EventType = AuditEventType.SystemAccess,
                Severity = AuditSeverity.Medium,
                IsEnabled = true,
                AutoEscalate = false,
                MatchCondition = (e) => {
                    var hour = e.Timestamp.Hour;
                    return Task.FromResult(e.EventType == AuditEventType.SystemAccess && (hour < 6 || hour > 22));
                }
            }
        });
    }

    private List<SecurityAnomalyDetection> DetectLoginAnomalies(List<ComprehensiveAuditEvent> events)
    {
        var anomalies = new List<SecurityAnomalyDetection>();
        var loginEvents = events.Where(e => e.EventType == AuditEventType.Authentication).ToList();

        // Multiple failed attempts from same IP
        var failedAttempts = loginEvents
            .Where(e => e.Details.Contains("failed", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => e.Source)
            .Where(g => g.Count() > _options.MaxFailedLoginsPerHour);

        foreach (var group in failedAttempts)
        {
            anomalies.Add(new SecurityAnomalyDetection
            {
                AnomalyType = "Excessive Failed Logins",
                Severity = AuditSeverity.High,
                Description = $"IP {group.Key} had {group.Count()} failed login attempts",
                DetectedAt = DateTime.UtcNow,
                RelatedEvents = group.ToList()
            });
        }

        return anomalies;
    }

    private List<SecurityAnomalyDetection> DetectPrivilegeAnomalies(List<ComprehensiveAuditEvent> events)
    {
        var anomalies = new List<SecurityAnomalyDetection>();
        var privilegeEvents = events.Where(e => e.EventType == AuditEventType.PrivilegeEscalation).ToList();

        foreach (var evt in privilegeEvents)
        {
            anomalies.Add(new SecurityAnomalyDetection
            {
                AnomalyType = "Privilege Escalation",
                Severity = AuditSeverity.Critical,
                Description = $"User {evt.UserId} attempted privilege escalation",
                DetectedAt = DateTime.UtcNow,
                RelatedEvents = new List<ComprehensiveAuditEvent> { evt }
            });
        }

        return anomalies;
    }

    private List<SecurityAnomalyDetection> DetectDataAccessAnomalies(List<ComprehensiveAuditEvent> events)
    {
        var anomalies = new List<SecurityAnomalyDetection>();
        var dataEvents = events.Where(e => e.EventType == AuditEventType.DataAccess).ToList();

        // Users accessing unusual amounts of data
        var heavyDataUsers = dataEvents
            .GroupBy(e => e.UserId)
            .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > _options.MaxDataAccessPerHour);

        foreach (var group in heavyDataUsers)
        {
            anomalies.Add(new SecurityAnomalyDetection
            {
                AnomalyType = "Excessive Data Access",
                Severity = AuditSeverity.Medium,
                Description = $"User {group.Key} accessed data {group.Count()} times",
                DetectedAt = DateTime.UtcNow,
                RelatedEvents = group.ToList()
            });
        }

        return anomalies;
    }

    private List<SecurityAnomalyDetection> DetectTimeBasedAnomalies(List<ComprehensiveAuditEvent> events)
    {
        var anomalies = new List<SecurityAnomalyDetection>();

        // Off-hours activity
        var offHoursEvents = events.Where(e =>
        {
            var hour = e.Timestamp.Hour;
            return hour < 6 || hour > 22;
        }).ToList();

        if (offHoursEvents.Count > _options.MaxOffHoursEvents)
        {
            anomalies.Add(new SecurityAnomalyDetection
            {
                AnomalyType = "Off-Hours Activity",
                Severity = AuditSeverity.Medium,
                Description = $"Detected {offHoursEvents.Count} events during off-hours",
                DetectedAt = DateTime.UtcNow,
                RelatedEvents = offHoursEvents
            });
        }

        return anomalies;
    }

    private List<SecurityAnomalyDetection> DetectGeographicAnomalies(List<ComprehensiveAuditEvent> events)
    {
        var anomalies = new List<SecurityAnomalyDetection>();

        // In a real implementation, you would use GeoIP to detect location-based anomalies
        // For now, just return empty list
        return anomalies;
    }

    private Dictionary<string, AccessPattern> AnalyzeAccessPatterns(List<ComprehensiveAuditEvent> events)
    {
        var patterns = new Dictionary<string, AccessPattern>();

        var accessEvents = events.Where(e => e.EventType == AuditEventType.SystemAccess ||
                                           e.EventType == AuditEventType.ApiAccess).ToList();

        var resourceAccess = accessEvents.GroupBy(e => e.Resource);

        foreach (var group in resourceAccess)
        {
            patterns[group.Key] = new AccessPattern
            {
                Resource = group.Key,
                AccessCount = group.Count(),
                UniqueUsers = group.Where(e => !string.IsNullOrEmpty(e.UserId))
                                  .Select(e => e.UserId).Distinct().Count(),
                PeakHour = group.GroupBy(e => e.Timestamp.Hour)
                               .OrderByDescending(g => g.Count())
                               .First().Key,
                LastAccessed = group.Max(e => e.Timestamp)
            };
        }

        return patterns;
    }

    private RiskAssessment PerformRiskAssessmentAsync(List<ComprehensiveAuditEvent> events)
    {
        var riskScore = 0.0;
        var riskFactors = new List<string>();

        // Critical events increase risk significantly
        var criticalEvents = events.Count(e => e.Severity == AuditSeverity.Critical);
        if (criticalEvents > 0)
        {
            riskScore += criticalEvents * 0.3;
            riskFactors.Add($"{criticalEvents} critical security events");
        }

        // High severity events
        var highEvents = events.Count(e => e.Severity == AuditSeverity.High);
        if (highEvents > 5)
        {
            riskScore += (highEvents - 5) * 0.1;
            riskFactors.Add($"{highEvents} high-severity events");
        }

        // Failed authentication attempts
        var failedLogins = events.Count(e => e.EventType == AuditEventType.Authentication &&
                                           e.Details.Contains("failed", StringComparison.OrdinalIgnoreCase));
        if (failedLogins > 10)
        {
            riskScore += (failedLogins - 10) * 0.05;
            riskFactors.Add($"{failedLogins} failed authentication attempts");
        }

        // Normalize risk score to 0-1 scale
        riskScore = Math.Min(1.0, riskScore);

        var riskLevel = riskScore switch
        {
            < 0.3 => RiskLevel.Low,
            < 0.6 => RiskLevel.Medium,
            < 0.8 => RiskLevel.High,
            _ => RiskLevel.Critical
        };

        return new RiskAssessment
        {
            RiskScore = riskScore,
            RiskLevel = riskLevel,
            RiskFactors = riskFactors,
            AssessedAt = DateTime.UtcNow
        };
    }

    private List<string> GenerateSecurityRecommendations(List<ComprehensiveAuditEvent> events, RiskAssessment riskAssessment)
    {
        var recommendations = new List<string>();

        if (riskAssessment.RiskLevel >= RiskLevel.High)
        {
            recommendations.Add("Immediate security review recommended due to high risk score");
        }

        var failedLogins = events.Count(e => e.EventType == AuditEventType.Authentication &&
                                           e.Details.Contains("failed", StringComparison.OrdinalIgnoreCase));
        if (failedLogins > 20)
        {
            recommendations.Add("Consider implementing additional authentication controls (e.g., account lockout, CAPTCHA)");
        }

        var privilegeEvents = events.Count(e => e.EventType == AuditEventType.PrivilegeEscalation);
        if (privilegeEvents > 0)
        {
            recommendations.Add("Review and strengthen privilege escalation controls");
        }

        var offHoursAccess = events.Count(e =>
        {
            var hour = e.Timestamp.Hour;
            return hour < 6 || hour > 22;
        });

        if (offHoursAccess > events.Count * 0.3) // More than 30% off-hours
        {
            recommendations.Add("Consider implementing time-based access controls");
        }

        return recommendations;
    }

    private ComplianceReport GenerateGDPRComplianceReport(List<ComprehensiveAuditEvent> events, ComplianceReport report)
    {
        // GDPR-specific compliance checks
        var dataAccessEvents = events.Where(e => e.EventType == AuditEventType.DataAccess).ToList();
        var dataModificationEvents = events.Where(e => e.EventType == AuditEventType.DataModification).ToList();

        report.ComplianceScore = 0.85; // Placeholder
        report.ComplianceIssues = new List<string>();

        if (dataAccessEvents.Any(e => string.IsNullOrEmpty(e.UserId)))
        {
            report.ComplianceIssues.Add("Anonymous data access detected - GDPR requires user identification");
        }

        return report;
    }

    private ComplianceReport GeneratePCIComplianceReport(List<ComprehensiveAuditEvent> events, ComplianceReport report)
    {
        // PCI DSS-specific compliance checks
        report.ComplianceScore = 0.90; // Placeholder
        report.ComplianceIssues = new List<string>();

        return report;
    }

    private ComplianceReport GenerateSOXComplianceReport(List<ComprehensiveAuditEvent> events, ComplianceReport report)
    {
        // SOX-specific compliance checks
        report.ComplianceScore = 0.88; // Placeholder
        report.ComplianceIssues = new List<string>();

        return report;
    }

    private ComplianceReport GenerateHIPAAComplianceReport(List<ComprehensiveAuditEvent> events, ComplianceReport report)
    {
        // HIPAA-specific compliance checks
        report.ComplianceScore = 0.92; // Placeholder
        report.ComplianceIssues = new List<string>();

        return report;
    }

    private List<ComprehensiveAuditEvent> GetAuditEventsForPeriod(DateTime start, DateTime end)
    {
        // In a real implementation, this would query a persistent audit store
        // For now, just return events from the queue that fall within the period
        return _auditQueue.Where(e => e.Timestamp >= start && e.Timestamp <= end).ToList();
    }

    private List<ComprehensiveAuditEvent> GetRecentAuditEvents(TimeSpan period)
    {
        var cutoff = DateTime.UtcNow.Subtract(period);
        return _auditQueue.Where(e => e.Timestamp >= cutoff).ToList();
    }

    private void ProcessCriticalEventAsync(ComprehensiveAuditEvent auditEvent)
    {
        // Immediate processing for critical security events
        _logger.LogCritical("CRITICAL SECURITY EVENT: {EventType} - {Details}",
            auditEvent.EventType, auditEvent.Details);

        // In a real implementation, you might:
        // - Send alerts to security team
        // - Trigger automated response actions
        // - Escalate to incident management system
        // - Block suspicious IPs temporarily

        if (_options.AutoResponseEnabled)
        {
            TriggerAutomatedResponseAsync(auditEvent);
        }
    }

    private void TriggerAutomatedResponseAsync(ComprehensiveAuditEvent auditEvent)
    {
        // Placeholder for automated security responses
        _logger.LogWarning("Automated security response triggered for event {AuditId}", auditEvent.AuditId);
    }

    private void EscalateSecurityIncidentAsync(ComprehensiveAuditEvent auditEvent, SecurityAuditRule rule)
    {
        // Placeholder for security incident escalation
        _logger.LogWarning("Security incident escalated: Rule {RuleName} triggered by event {AuditId}",
            rule.Name, auditEvent.AuditId);
    }

    private static string GenerateAuditId()
    {
        return $"AUDIT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    private void ProcessAuditEvents(object? state)
    {
        var processedCount = 0;
        var events = new List<ComprehensiveAuditEvent>();

        using var activity = SmartPayTracing.Source.StartActivity("ProcessAuditEvents");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            while (_auditQueue.TryDequeue(out var auditEvent) && processedCount < _options.MaxEventsPerBatch)
            {
                events.Add(auditEvent);
                processedCount++;
            }

            if (events.Count > 0)
            {
                // In a real implementation, you would persist these events to a database
                _logger.LogDebug("Processed {Count} audit events", events.Count);

                // Update security metrics
                UpdateSecurityMetrics(events);
            }
        }
        finally
        {
            _auditProcessingTime.Record(stopwatch.Elapsed.TotalSeconds);
        }
    }

    private void UpdateSecurityMetrics(List<ComprehensiveAuditEvent> events)
    {
        foreach (var evt in events)
        {
            var key = $"{evt.EventType}:{DateTime.UtcNow:yyyy-MM-dd-HH}";
            _securityMetrics.AddOrUpdate(key,
                new SecurityMetrics(1, DateTime.UtcNow),
                (_, existing) => existing with { Count = existing.Count + 1, LastUpdated = DateTime.UtcNow });
        }
    }

    private void GenerateSecurityReport(object? state)
    {
        _ = Task.Run(() =>
        {
            try
            {
                var report = GenerateAuditReportAsync();
                if (_options.AutoGenerateReports)
                {
                    SaveSecurityReportAsync(report);
                }

                var anomalies = DetectAnomaliesAsync();
                if (anomalies.Any(a => a.Severity >= AuditSeverity.High))
                {
                    _logger.LogWarning("Detected {Count} high-severity security anomalies",
                        anomalies.Count(a => a.Severity >= AuditSeverity.High));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate security report");
            }
        });
    }

    private void SaveSecurityReportAsync(SecurityAuditReport report)
    {
        // In a real implementation, you would save the report to persistent storage
        var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation("Security audit report generated: {ReportId}", report.ReportId);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Comprehensive Security Audit Service started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Comprehensive Security Audit Service stopping");
        _auditTimer?.Change(Timeout.Infinite, 0);
        _reportTimer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _auditTimer?.Dispose();
        _reportTimer?.Dispose();
    }
}

// Supporting classes and enums...
public class SecurityAuditOptions
{
    public bool AutoGenerateReports { get; set; } = true;
    public bool AutoResponseEnabled { get; set; } = false;
    public int MaxEventsPerBatch { get; set; } = 1000;
    public int MaxFailedLoginsPerHour { get; set; } = 5;
    public int MaxDataAccessPerHour { get; set; } = 100;
    public int MaxOffHoursEvents { get; set; } = 50;
    public TimeSpan ReportGenerationInterval { get; set; } = TimeSpan.FromMinutes(30);
}

public class ComprehensiveAuditEvent
{
    public string AuditId { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public AuditEventType EventType { get; set; }
    public AuditSeverity Severity { get; set; }
    public string? UserId { get; set; }
    public string Source { get; set; } = "";
    public string Resource { get; set; } = "";
    public string Details { get; set; } = "";
    public Dictionary<string, object> AdditionalData { get; set; } = new();
    public List<string> TriggeredRules { get; set; } = new();
}

public class SecurityAuditRule
{
    public string Name { get; set; } = "";
    public AuditEventType EventType { get; set; }
    public AuditSeverity Severity { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool AutoEscalate { get; set; } = false;
    public Func<ComprehensiveAuditEvent, Task<bool>> MatchCondition { get; set; } = _ => Task.FromResult(false);

    public async Task<bool> MatchesAsync(ComprehensiveAuditEvent auditEvent)
    {
        return await MatchCondition(auditEvent);
    }
}

public class SecurityAuditReport
{
    public string ReportId { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int TotalEvents { get; set; }
    public Dictionary<string, int> EventsByType { get; set; } = new();
    public Dictionary<string, int> EventsBySeverity { get; set; } = new();
    public int SecurityViolations { get; set; }
    public int CriticalIncidents { get; set; }
    public Dictionary<string, UserActivitySummary> UserActivitySummary { get; set; } = new();
    public Dictionary<string, AccessPattern> AccessPatterns { get; set; } = new();
    public RiskAssessment RiskAssessment { get; set; } = new();
    public List<string> SecurityRecommendations { get; set; } = new();
}

public class SecurityAnomalyDetection
{
    public string AnomalyType { get; set; } = "";
    public AuditSeverity Severity { get; set; }
    public string Description { get; set; } = "";
    public DateTime DetectedAt { get; set; }
    public List<ComprehensiveAuditEvent> RelatedEvents { get; set; } = new();
}

public class ComplianceReport
{
    public ComplianceFramework Framework { get; set; }
    public DateTime GeneratedAt { get; set; }
    public TimeSpan AssessmentPeriod { get; set; }
    public double ComplianceScore { get; set; }
    public List<string> ComplianceIssues { get; set; } = new();
}

public class UserActivitySummary
{
    public string UserId { get; set; } = "";
    public int EventCount { get; set; }
    public DateTime LastActivity { get; set; }
    public int ViolationCount { get; set; }
}

public class AccessPattern
{
    public string Resource { get; set; } = "";
    public int AccessCount { get; set; }
    public int UniqueUsers { get; set; }
    public int PeakHour { get; set; }
    public DateTime LastAccessed { get; set; }
}

public class RiskAssessment
{
    public double RiskScore { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public List<string> RiskFactors { get; set; } = new();
    public DateTime AssessedAt { get; set; }
}

public record SecurityMetrics(int Count, DateTime LastUpdated);

public enum AuditEventType
{
    Authentication,
    Authorization,
    DataAccess,
    DataModification,
    ConfigurationChange,
    SecurityViolation,
    SystemAccess,
    PrivilegeEscalation,
    ApiAccess,
    FileAccess
}

public enum AuditSeverity
{
    Low,
    Medium,
    High,
    Critical
}


public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

// NOTE: Compliance enums are centralized in RichMove.SmartPay.Core.Compliance.