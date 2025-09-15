using Microsoft.Extensions.Options;
using RichMove.SmartPay.Core.Compliance;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace RichMove.SmartPay.Api.Compliance;

public sealed partial class ComplianceMonitoringService : IHostedService, IDisposable
{
    private readonly ILogger<ComplianceMonitoringService> _logger;
    private readonly ComplianceOptions _options;
    private readonly Timer _complianceTimer;
    private readonly ConcurrentQueue<ComplianceEvent> _eventQueue;
    private readonly Dictionary<ComplianceFramework, ComplianceStatus> _frameworkStatus;
    private readonly Meter _meter;

    // Compliance metrics
    private readonly Counter<long> _complianceViolations;
    private readonly Counter<long> _auditEvents;
    private readonly Gauge<long> _activeAlerts;

    public ComplianceMonitoringService(
        ILogger<ComplianceMonitoringService> logger,
        IOptions<ComplianceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        _options = options.Value;
        _eventQueue = new ConcurrentQueue<ComplianceEvent>();
        _frameworkStatus = [];

        _meter = new Meter("richmove.smartpay.compliance");

        _complianceViolations = _meter.CreateCounter<long>(
            "richmove_smartpay_compliance_violations_total",
            "violations",
            "Total number of compliance violations");

        _auditEvents = _meter.CreateCounter<long>(
            "richmove_smartpay_audit_events_total",
            "events",
            "Total number of audit events");

        _activeAlerts = _meter.CreateGauge<long>(
            "richmove_smartpay_compliance_alerts_active",
            "alerts",
            "Number of active compliance alerts");

        InitializeFrameworkStatus();

        _complianceTimer = new Timer(ProcessComplianceChecks, null,
            TimeSpan.FromSeconds(30), _options.CheckInterval);

        Log.ComplianceMonitoringInitialized(_logger, _options.EnabledFrameworks.Count);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Log.ComplianceMonitoringStarted(_logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Log.ComplianceMonitoringStopped(_logger);
        return Task.CompletedTask;
    }

    private void InitializeFrameworkStatus()
    {
        foreach (var framework in _options.EnabledFrameworks)
        {
            _frameworkStatus[framework] = new ComplianceStatus
            {
                Framework = framework,
                Status = ComplianceState.Unknown,
                LastChecked = DateTime.MinValue,
                Violations = []
            };
        }
    }

    private async void ProcessComplianceChecks(object? state)
    {
        try
        {
            var checkStart = DateTime.UtcNow;

            foreach (var framework in _options.EnabledFrameworks)
            {
                await CheckFrameworkCompliance(framework);
            }

            // Process queued audit events
            await ProcessAuditEventQueue();

            Log.ComplianceCheckCompleted(_logger,
                _options.EnabledFrameworks.Count,
                (DateTime.UtcNow - checkStart).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            Log.ComplianceCheckFailed(_logger, ex);
        }
    }

    private async Task CheckFrameworkCompliance(ComplianceFramework framework)
    {
        var status = _frameworkStatus[framework];
        var violations = new List<ComplianceViolation>();

        try
        {
            switch (framework)
            {
                case ComplianceFramework.PCIDSS:
                    violations.AddRange(await CheckPciDssCompliance());
                    break;

                case ComplianceFramework.GDPR:
                    violations.AddRange(await CheckGdprCompliance());
                    break;

                case ComplianceFramework.SOX:
                    violations.AddRange(await CheckSoxCompliance());
                    break;

                case ComplianceFramework.ISO27001:
                    violations.AddRange(await CheckIso27001Compliance());
                    break;

                    // NIST framework not included in canonical implementation
            }

            status.Status = violations.Count == 0 ? ComplianceState.Passing : ComplianceState.Failing;
            status.Violations = violations;
            status.LastChecked = DateTime.UtcNow;

            if (violations.Count > 0)
            {
                _complianceViolations.Add(violations.Count,
                    new KeyValuePair<string, object?>("framework", framework.ToString()));

                LogComplianceViolations(framework, violations);
            }

            Log.FrameworkComplianceChecked(_logger, framework.ToString(), violations.Count);
        }
        catch (Exception ex)
        {
            status.Status = ComplianceState.Failing;
            status.LastError = ex.Message;
            Log.FrameworkComplianceCheckFailed(_logger, framework.ToString(), ex);
        }
    }

    private async Task<List<ComplianceViolation>> CheckPciDssCompliance()
    {
        var violations = new List<ComplianceViolation>();

        // PCI DSS Requirement 1: Firewall Configuration
        if (!await CheckFirewallConfiguration())
        {
            violations.Add(new ComplianceViolation
            {
                Requirement = "PCI DSS 1.1",
                Description = "Firewall configuration standards not met",
                Severity = ComplianceSeverity.High,
                DetectedAt = DateTime.UtcNow
            });
        }

        // PCI DSS Requirement 2: Default passwords
        if (!CheckDefaultPasswordPolicy())
        {
            violations.Add(new ComplianceViolation
            {
                Requirement = "PCI DSS 2.1",
                Description = "Default passwords detected in system",
                Severity = ComplianceSeverity.Critical,
                DetectedAt = DateTime.UtcNow
            });
        }

        // PCI DSS Requirement 4: Data transmission encryption
        if (!CheckDataEncryptionInTransit())
        {
            violations.Add(new ComplianceViolation
            {
                Requirement = "PCI DSS 4.1",
                Description = "Unencrypted data transmission detected",
                Severity = ComplianceSeverity.High,
                DetectedAt = DateTime.UtcNow
            });
        }

        // PCI DSS Requirement 10: Logging and monitoring
        if (!CheckAuditLogging())
        {
            violations.Add(new ComplianceViolation
            {
                Requirement = "PCI DSS 10.2",
                Description = "Insufficient audit logging configuration",
                Severity = ComplianceSeverity.Medium,
                DetectedAt = DateTime.UtcNow
            });
        }

        return violations;
    }

    private async Task<List<ComplianceViolation>> CheckGdprCompliance()
    {
        var violations = new List<ComplianceViolation>();

        // GDPR Article 25: Data protection by design
        if (!await CheckDataProtectionByDesign())
        {
            violations.Add(new ComplianceViolation
            {
                Requirement = "GDPR Art. 25",
                Description = "Data protection by design principles not implemented",
                Severity = ComplianceSeverity.High,
                DetectedAt = DateTime.UtcNow
            });
        }

        // GDPR Article 30: Records of processing activities
        if (!CheckProcessingRecords())
        {
            violations.Add(new ComplianceViolation
            {
                Requirement = "GDPR Art. 30",
                Description = "Incomplete records of processing activities",
                Severity = ComplianceSeverity.Medium,
                DetectedAt = DateTime.UtcNow
            });
        }

        // GDPR Article 32: Security of processing
        if (!CheckProcessingSecurity())
        {
            violations.Add(new ComplianceViolation
            {
                Requirement = "GDPR Art. 32",
                Description = "Insufficient security measures for data processing",
                Severity = ComplianceSeverity.High,
                DetectedAt = DateTime.UtcNow
            });
        }

        return violations;
    }

    private async Task<List<ComplianceViolation>> CheckSoxCompliance()
    {
        var violations = new List<ComplianceViolation>();

        // SOX Section 302: Financial reporting controls
        if (!await CheckFinancialReportingControls())
        {
            violations.Add(new ComplianceViolation
            {
                Requirement = "SOX 302",
                Description = "Financial reporting controls not adequately documented",
                Severity = ComplianceSeverity.High,
                DetectedAt = DateTime.UtcNow
            });
        }

        // SOX Section 404: Internal control assessment
        if (!CheckInternalControlAssessment())
        {
            violations.Add(new ComplianceViolation
            {
                Requirement = "SOX 404",
                Description = "Internal control assessment incomplete",
                Severity = ComplianceSeverity.Medium,
                DetectedAt = DateTime.UtcNow
            });
        }

        return violations;
    }

    private async Task<List<ComplianceViolation>> CheckIso27001Compliance()
    {
        var violations = new List<ComplianceViolation>();

        // ISO 27001 A.12.1: Operational procedures
        if (!await CheckOperationalProcedures())
        {
            violations.Add(new ComplianceViolation
            {
                Requirement = "ISO 27001 A.12.1",
                Description = "Operational procedures not documented or followed",
                Severity = ComplianceSeverity.Medium,
                DetectedAt = DateTime.UtcNow
            });
        }

        // ISO 27001 A.12.6: Management of technical vulnerabilities
        if (!CheckVulnerabilityManagement())
        {
            violations.Add(new ComplianceViolation
            {
                Requirement = "ISO 27001 A.12.6",
                Description = "Technical vulnerability management process inadequate",
                Severity = ComplianceSeverity.High,
                DetectedAt = DateTime.UtcNow
            });
        }

        return violations;
    }

    private async Task<List<ComplianceViolation>> CheckNistCompliance()
    {
        var violations = new List<ComplianceViolation>();

        // NIST Cybersecurity Framework: Identify
        if (!await CheckAssetIdentification())
        {
            violations.Add(new ComplianceViolation
            {
                Requirement = "NIST CSF ID.AM",
                Description = "Asset management and identification incomplete",
                Severity = ComplianceSeverity.Medium,
                DetectedAt = DateTime.UtcNow
            });
        }

        // NIST Cybersecurity Framework: Protect
        if (!CheckAccessControlProtection())
        {
            violations.Add(new ComplianceViolation
            {
                Requirement = "NIST CSF PR.AC",
                Description = "Access control measures insufficient",
                Severity = ComplianceSeverity.High,
                DetectedAt = DateTime.UtcNow
            });
        }

        return violations;
    }

    // Compliance check implementations (simplified for demo)
    private static async Task<bool> CheckFirewallConfiguration()
    {
        await Task.Delay(50);
        return Random.Shared.Next(100) > 10; // 90% compliance rate
    }

    private static bool CheckDefaultPasswordPolicy()
    {
        return Random.Shared.Next(100) > 5; // 95% compliance rate
    }

    private static bool CheckDataEncryptionInTransit()
    {
        // Check if HTTPS is properly configured
        return !Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Contains("http://") ?? true;
    }

    private static bool CheckAuditLogging()
    {
        // Check if logging is properly configured
        return true; // Our logging is properly configured
    }

    private static async Task<bool> CheckDataProtectionByDesign()
    {
        await Task.Delay(30);
        return Random.Shared.Next(100) > 20; // 80% compliance rate
    }

    private static bool CheckProcessingRecords()
    {
        return Random.Shared.Next(100) > 15; // 85% compliance rate
    }

    private static bool CheckProcessingSecurity()
    {
        return Random.Shared.Next(100) > 10; // 90% compliance rate
    }

    private static async Task<bool> CheckFinancialReportingControls()
    {
        await Task.Delay(40);
        return Random.Shared.Next(100) > 25; // 75% compliance rate
    }

    private static bool CheckInternalControlAssessment()
    {
        return Random.Shared.Next(100) > 30; // 70% compliance rate
    }

    private static async Task<bool> CheckOperationalProcedures()
    {
        await Task.Delay(35);
        return Random.Shared.Next(100) > 20; // 80% compliance rate
    }

    private static bool CheckVulnerabilityManagement()
    {
        return Random.Shared.Next(100) > 15; // 85% compliance rate
    }

    private static async Task<bool> CheckAssetIdentification()
    {
        await Task.Delay(25);
        return Random.Shared.Next(100) > 10; // 90% compliance rate
    }

    private static bool CheckAccessControlProtection()
    {
        return Random.Shared.Next(100) > 5; // 95% compliance rate
    }

    private void LogComplianceViolations(ComplianceFramework framework, List<ComplianceViolation> violations)
    {
        foreach (var violation in violations)
        {
            var auditEvent = new ComplianceEvent
            {
                Id = Guid.NewGuid().ToString(),
                Type = "ComplianceViolation",
                Framework = framework.ToString(),
                Timestamp = violation.DetectedAt,
                Severity = violation.Severity.ToString(),
                Details = JsonSerializer.Serialize(violation),
                UserId = "system",
                Source = "ComplianceMonitoring"
            };

            _eventQueue.Enqueue(auditEvent);
            _auditEvents.Add(1,
                new KeyValuePair<string, object?>("framework", framework.ToString()),
                new KeyValuePair<string, object?>("severity", violation.Severity.ToString()));
        }

    }

    private async Task ProcessAuditEventQueue()
    {
        var processedEvents = 0;
        var maxProcessCount = 100;

        while (_eventQueue.TryDequeue(out var auditEvent) && processedEvents < maxProcessCount)
        {
            try
            {
                await ProcessAuditEvent(auditEvent);
                processedEvents++;
            }
            catch (Exception ex)
            {
                Log.AuditEventProcessingFailed(_logger, auditEvent.Id, ex);
                break;
            }
        }

        if (processedEvents > 0)
        {
            Log.AuditEventsProcessed(_logger, processedEvents);
        }
    }

    private async Task ProcessAuditEvent(ComplianceEvent auditEvent)
    {
        // In production, this would write to secure audit log storage
        await Task.Delay(10);

        Log.AuditEventRecorded(_logger, auditEvent.Type, auditEvent.Framework,
            auditEvent.Severity, auditEvent.UserId);
    }

    public void RecordComplianceEvent(string eventType, string framework, string severity,
        string details, string? userId = null)
    {
        var auditEvent = new ComplianceEvent
        {
            Id = Guid.NewGuid().ToString(),
            Type = eventType,
            Framework = framework,
            Timestamp = DateTime.UtcNow,
            Severity = severity,
            Details = details,
            UserId = userId ?? "anonymous",
            Source = "Application"
        };

        _eventQueue.Enqueue(auditEvent);
        _auditEvents.Add(1,
            new KeyValuePair<string, object?>("framework", framework),
            new KeyValuePair<string, object?>("severity", severity));
    }

    public ComplianceReport GetComplianceReport()
    {
        var activeAlertCount = _frameworkStatus.Values
            .Sum(s => s.Violations.Count(v => v.Severity >= ComplianceSeverity.High));

        _activeAlerts.Record(activeAlertCount);

        return new ComplianceReport
        {
            GeneratedAt = DateTime.UtcNow,
            FrameworkStatus = _frameworkStatus.Values.ToList(),
            TotalViolations = _frameworkStatus.Values.Sum(s => s.Violations.Count),
            CriticalViolations = _frameworkStatus.Values
                .Sum(s => s.Violations.Count(v => v.Severity == ComplianceSeverity.Critical)),
            ComplianceScore = CalculateComplianceScore()
        };
    }

    private double CalculateComplianceScore()
    {
        if (!_frameworkStatus.Any())
            return 100.0;

        var totalFrameworks = _frameworkStatus.Count;
        var compliantFrameworks = _frameworkStatus.Values
            .Count(s => s.Status == ComplianceState.Passing);

        return (double)compliantFrameworks / totalFrameworks * 100.0;
    }

    public void Dispose()
    {
        _complianceTimer?.Dispose();
        _meter?.Dispose();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 9101, Level = LogLevel.Information,
            Message = "Compliance monitoring initialized ({FrameworkCount} frameworks enabled)")]
        public static partial void ComplianceMonitoringInitialized(ILogger logger, int frameworkCount);

        [LoggerMessage(EventId = 9102, Level = LogLevel.Information,
            Message = "Compliance monitoring started")]
        public static partial void ComplianceMonitoringStarted(ILogger logger);

        [LoggerMessage(EventId = 9103, Level = LogLevel.Information,
            Message = "Compliance monitoring stopped")]
        public static partial void ComplianceMonitoringStopped(ILogger logger);

        [LoggerMessage(EventId = 9104, Level = LogLevel.Debug,
            Message = "Compliance check completed: {FrameworkCount} frameworks in {DurationMs}ms")]
        public static partial void ComplianceCheckCompleted(ILogger logger, int frameworkCount, double durationMs);

        [LoggerMessage(EventId = 9105, Level = LogLevel.Error,
            Message = "Compliance check failed")]
        public static partial void ComplianceCheckFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9106, Level = LogLevel.Debug,
            Message = "Framework compliance checked: {Framework} ({ViolationCount} violations)")]
        public static partial void FrameworkComplianceChecked(ILogger logger, string framework, int violationCount);

        [LoggerMessage(EventId = 9107, Level = LogLevel.Error,
            Message = "Framework compliance check failed: {Framework}")]
        public static partial void FrameworkComplianceCheckFailed(ILogger logger, string framework, Exception exception);

        [LoggerMessage(EventId = 9108, Level = LogLevel.Information,
            Message = "Audit events processed: {EventCount}")]
        public static partial void AuditEventsProcessed(ILogger logger, int eventCount);

        [LoggerMessage(EventId = 9109, Level = LogLevel.Error,
            Message = "Audit event processing failed: {EventId}")]
        public static partial void AuditEventProcessingFailed(ILogger logger, string eventId, Exception exception);

        [LoggerMessage(EventId = 9110, Level = LogLevel.Information,
            Message = "Audit event recorded: {Type} ({Framework}) - {Severity} by {UserId}")]
        public static partial void AuditEventRecorded(ILogger logger, string type, string framework,
            string severity, string userId);
    }
}

// Supporting types
public sealed class ComplianceOptions
{
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(1);
    // Keep default frameworks but ensure uniqueness via Normalize().
    public List<ComplianceFramework> EnabledFrameworks { get; } =
        new() { ComplianceFramework.PCIDSS, ComplianceFramework.GDPR };
    public bool EnableAuditLogging { get; set; } = true;
    public bool EnableRealTimeMonitoring { get; set; } = true;

    /// <summary>
    /// Ensures unique frameworks and removes None entries after config binding.
    /// Call once during service startup.
    /// </summary>
    public void Normalize()
    {
        if (EnabledFrameworks.Count == 0) return;
        var distinct = EnabledFrameworks
            .Where(f => f != ComplianceFramework.None)
            .Distinct()
            .ToList();
        if (distinct.Count != EnabledFrameworks.Count)
        {
            EnabledFrameworks.Clear();
            EnabledFrameworks.AddRange(distinct);
        }
    }
}


public sealed class ComplianceStatus
{
    public ComplianceFramework Framework { get; set; }
    public ComplianceState Status { get; set; }
    public DateTime LastChecked { get; set; }
    public List<ComplianceViolation> Violations { get; set; } = [];
    public string? LastError { get; set; }
}

public sealed class ComplianceViolation
{
    public string Requirement { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ComplianceSeverity Severity { get; set; }
    public DateTime DetectedAt { get; set; }
}

public sealed class ComplianceEvent
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public sealed class ComplianceReport
{
    public DateTime GeneratedAt { get; set; }
    public List<ComplianceStatus> FrameworkStatus { get; set; } = [];
    public int TotalViolations { get; set; }
    public int CriticalViolations { get; set; }
    public double ComplianceScore { get; set; }
}

// NOTE: Compliance enums are centralized in RichMove.SmartPay.Core.Compliance.