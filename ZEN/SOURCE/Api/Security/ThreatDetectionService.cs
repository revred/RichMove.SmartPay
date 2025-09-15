using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;

namespace RichMove.SmartPay.Api.Security;

public sealed partial class ThreatDetectionService : IHostedService, IDisposable
{
    private readonly ILogger<ThreatDetectionService> _logger;
    private readonly ThreatDetectionOptions _options;
    private readonly Timer _analysisTimer;
    private readonly ConcurrentQueue<SecurityEvent> _eventQueue;
    private readonly Dictionary<string, ThreatPattern> _threatPatterns;
    private readonly Dictionary<string, ClientThreatProfile> _clientProfiles;
    private readonly Meter _meter;

    // Threat detection metrics
    private readonly Counter<long> _threatsDetected;
    private readonly Counter<long> _securityEvents;
    private readonly Gauge<long> _activeThreatLevel;
    private readonly Histogram<double> _threatAnalysisTime;

    private readonly Dictionary<ThreatType, int> _threatCounts;
    private DateTime _lastAnalysis = DateTime.MinValue;

    public ThreatDetectionService(
        ILogger<ThreatDetectionService> logger,
        IOptions<ThreatDetectionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        _options = options.Value;
        _eventQueue = new ConcurrentQueue<SecurityEvent>();
        _threatPatterns = [];
        _clientProfiles = [];
        _threatCounts = [];

        _meter = new Meter("richmove.smartpay.threats");

        _threatsDetected = _meter.CreateCounter<long>(
            "richmove_smartpay_threats_detected_total",
            "threats",
            "Total number of threats detected");

        _securityEvents = _meter.CreateCounter<long>(
            "richmove_smartpay_security_events_total",
            "events",
            "Total number of security events processed");

        _activeThreatLevel = _meter.CreateGauge<long>(
            "richmove_smartpay_threat_level_active",
            "level",
            "Current active threat level (1-5)");

        _threatAnalysisTime = _meter.CreateHistogram<double>(
            "richmove_smartpay_threat_analysis_duration_seconds",
            "seconds",
            "Time taken to analyze threats");

        InitializeThreatPatterns();

        _analysisTimer = new Timer(AnalyzeThreatData, null,
            TimeSpan.FromSeconds(10), _options.AnalysisInterval);

        Log.ThreatDetectionServiceInitialized(_logger, _threatPatterns.Count);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Log.ThreatDetectionServiceStarted(_logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Log.ThreatDetectionServiceStopped(_logger);
        return Task.CompletedTask;
    }

    private void InitializeThreatPatterns()
    {
        // SQL Injection patterns
        _threatPatterns["sql_injection"] = new ThreatPattern
        {
            Id = "sql_injection",
            Type = ThreatType.SqlInjection,
            Severity = ThreatSeverity.High,
            Patterns = [
                @"(\'\s*(\|\||\||\&\&|\&)\s*\')",
                @"(\'\s*(;|--|\#)\s*)",
                @"(\bUNION\b.*\bSELECT\b)",
                @"(\bSELECT\b.*\bFROM\b.*\bWHERE\b.*\b=\b.*\bOR\b)",
                @"(\'\s*\)\s*\;\s*DROP\s+TABLE\b)"
            ],
            Description = "Potential SQL injection attack detected"
        };

        // XSS patterns
        _threatPatterns["xss_attack"] = new ThreatPattern
        {
            Id = "xss_attack",
            Type = ThreatType.CrossSiteScripting,
            Severity = ThreatSeverity.Medium,
            Patterns = [
                @"<script[^>]*>.*?</script>",
                @"javascript\s*:",
                @"on\w+\s*=",
                @"<iframe[^>]*>.*?</iframe>",
                @"<object[^>]*>.*?</object>"
            ],
            Description = "Potential XSS attack detected"
        };

        // Brute force patterns
        _threatPatterns["brute_force"] = new ThreatPattern
        {
            Id = "brute_force",
            Type = ThreatType.BruteForce,
            Severity = ThreatSeverity.High,
            Patterns = [
                @"(failed\s+login.*){3,}",
                @"(invalid\s+credentials.*){3,}",
                @"(authentication\s+failure.*){3,}"
            ],
            Description = "Potential brute force attack detected"
        };

        // Directory traversal patterns
        _threatPatterns["directory_traversal"] = new ThreatPattern
        {
            Id = "directory_traversal",
            Type = ThreatType.DirectoryTraversal,
            Severity = ThreatSeverity.Medium,
            Patterns = [
                @"\.\./",
                @"\.\.\\",
                @"%2e%2e%2f",
                @"%2e%2e%5c"
            ],
            Description = "Potential directory traversal attack detected"
        };

        // Command injection patterns
        _threatPatterns["command_injection"] = new ThreatPattern
        {
            Id = "command_injection",
            Type = ThreatType.CommandInjection,
            Severity = ThreatSeverity.Critical,
            Patterns = [
                @"[\;\|\&\$\`]",
                @"(cmd|powershell|bash|sh)\s+",
                @"(rm|del|format)\s+",
                @">\s*/dev/null"
            ],
            Description = "Potential command injection attack detected"
        };

        // DDoS patterns
        _threatPatterns["ddos_attack"] = new ThreatPattern
        {
            Id = "ddos_attack",
            Type = ThreatType.DenialOfService,
            Severity = ThreatSeverity.Critical,
            Patterns = [
                @"(GET|POST|PUT|DELETE).*\s+HTTP/1\.[01].*Connection:\s*close.*{10,}",
                @"User-Agent:\s*(curl|wget|httping).*{5,}"
            ],
            Description = "Potential DDoS attack detected"
        };
    }

    private async void AnalyzeThreatData(object? state)
    {
        var analysisStart = DateTime.UtcNow;

        try
        {
            var eventsProcessed = await ProcessSecurityEventQueue();
            await AnalyzeClientBehaviorPatterns();
            await UpdateThreatLevel();

            _lastAnalysis = analysisStart;

            var analysisDuration = (DateTime.UtcNow - analysisStart).TotalSeconds;
            _threatAnalysisTime.Record(analysisDuration);

            Log.ThreatAnalysisCompleted(_logger, eventsProcessed, analysisDuration);
        }
        catch (Exception ex)
        {
            Log.ThreatAnalysisFailed(_logger, ex);
        }
    }

    private async Task<int> ProcessSecurityEventQueue()
    {
        var processedCount = 0;
        var maxProcessCount = 1000;

        while (_eventQueue.TryDequeue(out var securityEvent) && processedCount < maxProcessCount)
        {
            try
            {
                await ProcessSecurityEvent(securityEvent);
                processedCount++;

                _securityEvents.Add(1,
                    new KeyValuePair<string, object?>("source", securityEvent.Source),
                    new KeyValuePair<string, object?>("severity", securityEvent.Severity.ToString()));
            }
            catch (Exception ex)
            {
                Log.SecurityEventProcessingFailed(_logger, securityEvent.Id, ex);
                break;
            }
        }

        return processedCount;
    }

    private async Task ProcessSecurityEvent(SecurityEvent securityEvent)
    {
        var threats = await AnalyzeEventForThreats(securityEvent);

        foreach (var threat in threats)
        {
            await HandleDetectedThreat(threat, securityEvent);
        }

        // Update client profile
        UpdateClientProfile(securityEvent);
    }

    private async Task<List<DetectedThreat>> AnalyzeEventForThreats(SecurityEvent securityEvent)
    {
        var detectedThreats = new List<DetectedThreat>();

        foreach (var pattern in _threatPatterns.Values)
        {
            if (await CheckThreatPattern(securityEvent, pattern))
            {
                var threat = new DetectedThreat
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = pattern.Type,
                    Severity = pattern.Severity,
                    Description = pattern.Description,
                    EventId = securityEvent.Id,
                    ClientId = securityEvent.ClientId,
                    DetectedAt = DateTime.UtcNow,
                    SourceData = securityEvent.Data,
                    Confidence = CalculateThreatConfidence(securityEvent, pattern)
                };

                detectedThreats.Add(threat);

                Log.ThreatDetected(_logger, threat.Type.ToString(), threat.Severity.ToString(),
                    threat.ClientId, threat.Confidence);
            }
        }

        return detectedThreats;
    }

    private async Task<bool> CheckThreatPattern(SecurityEvent securityEvent, ThreatPattern pattern)
    {
        try
        {
            var dataToAnalyze = securityEvent.Data ?? string.Empty;

            foreach (var patternRegex in pattern.Patterns)
            {
                if (Regex.IsMatch(dataToAnalyze, patternRegex, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
                {
                    await Task.Delay(1); // Simulate pattern analysis time
                    return true;
                }
            }

            // Additional behavioral analysis
            return await AnalyzeBehavioralPatterns(securityEvent, pattern);
        }
        catch (RegexMatchTimeoutException)
        {
            Log.ThreatPatternTimeout(_logger, pattern.Id);
            return false;
        }
        catch (Exception ex)
        {
            Log.ThreatPatternAnalysisFailed(_logger, pattern.Id, ex);
            return false;
        }
    }

    private async Task<bool> AnalyzeBehavioralPatterns(SecurityEvent securityEvent, ThreatPattern pattern)
    {
        // Behavioral pattern analysis based on client history
        if (_clientProfiles.TryGetValue(securityEvent.ClientId, out var profile))
        {
            switch (pattern.Type)
            {
                case ThreatType.BruteForce:
                    return await CheckBruteForcePattern(profile);

                case ThreatType.DenialOfService:
                    return CheckDdosPattern(profile);

                case ThreatType.AnomalousActivity:
                    return CheckAnomalousActivity(profile, securityEvent);

                default:
                    return false;
            }
        }

        return false;
    }

    private async Task<bool> CheckBruteForcePattern(ClientThreatProfile profile)
    {
        await Task.Delay(5);

        var recentFailures = profile.Events
            .Where(e => e.Timestamp > DateTime.UtcNow.AddMinutes(-5))
            .Count(e => e.Data?.Contains("failed", StringComparison.OrdinalIgnoreCase) == true);

        return recentFailures >= _options.BruteForceThreshold;
    }

    private static bool CheckDdosPattern(ClientThreatProfile profile)
    {
        var recentRequests = profile.Events
            .Count(e => e.Timestamp > DateTime.UtcNow.AddMinutes(-1));

        return recentRequests >= 100; // 100 requests per minute threshold
    }

    private static bool CheckAnomalousActivity(ClientThreatProfile profile, SecurityEvent currentEvent)
    {
        // Check for unusual request patterns
        var avgRequestSize = profile.Events.Average(e => e.Data?.Length ?? 0);
        var currentSize = currentEvent.Data?.Length ?? 0;

        return currentSize > avgRequestSize * 3; // 3x average size
    }

    private static double CalculateThreatConfidence(SecurityEvent securityEvent, ThreatPattern pattern)
    {
        var confidence = 0.5; // Base confidence

        // Increase confidence based on pattern complexity
        confidence += pattern.Patterns.Count * 0.1;

        // Increase confidence based on event severity
        confidence += (int)securityEvent.Severity * 0.15;

        // Cap at 1.0
        return Math.Min(confidence, 1.0);
    }

    private async Task HandleDetectedThreat(DetectedThreat threat, SecurityEvent securityEvent)
    {
        // Record threat detection metrics
        _threatsDetected.Add(1,
            new KeyValuePair<string, object?>("type", threat.Type.ToString()),
            new KeyValuePair<string, object?>("severity", threat.Severity.ToString()));

        // Update threat counts
        _threatCounts[threat.Type] = _threatCounts.GetValueOrDefault(threat.Type, 0) + 1;

        // Execute threat response based on severity
        await ExecuteThreatResponse(threat, securityEvent);

        Log.ThreatHandled(_logger, threat.Id, threat.Type.ToString(),
            threat.ClientId, threat.Confidence);
    }

    private async Task ExecuteThreatResponse(DetectedThreat threat, SecurityEvent securityEvent)
    {
        switch (threat.Severity)
        {
            case ThreatSeverity.Critical:
                await ExecuteCriticalThreatResponse(threat, securityEvent);
                break;

            case ThreatSeverity.High:
                await ExecuteHighThreatResponse(threat, securityEvent);
                break;

            case ThreatSeverity.Medium:
                await ExecuteMediumThreatResponse(threat, securityEvent);
                break;

            case ThreatSeverity.Low:
                await ExecuteLowThreatResponse(threat, securityEvent);
                break;
        }
    }

    private async Task ExecuteCriticalThreatResponse(DetectedThreat threat, SecurityEvent securityEvent)
    {
        // Critical threats: Immediate blocking and alerting
        await BlockClientImmediately(threat.ClientId);
        await TriggerSecurityAlert(threat, AlertLevel.Critical);
        await NotifySecurityTeam(threat);

        Log.CriticalThreatResponse(_logger, threat.Id, threat.ClientId);
    }

    private async Task ExecuteHighThreatResponse(DetectedThreat threat, SecurityEvent securityEvent)
    {
        // High threats: Rate limiting and monitoring
        await ApplyRateLimiting(threat.ClientId, RateLimitLevel.Strict);
        await TriggerSecurityAlert(threat, AlertLevel.High);
        await EnhanceMonitoring(threat.ClientId);

        Log.HighThreatResponse(_logger, threat.Id, threat.ClientId);
    }

    private async Task ExecuteMediumThreatResponse(DetectedThreat threat, SecurityEvent securityEvent)
    {
        // Medium threats: Enhanced logging and monitoring
        await ApplyRateLimiting(threat.ClientId, RateLimitLevel.Moderate);
        await TriggerSecurityAlert(threat, AlertLevel.Medium);

        Log.MediumThreatResponse(_logger, threat.Id, threat.ClientId);
    }

    private async Task ExecuteLowThreatResponse(DetectedThreat threat, SecurityEvent securityEvent)
    {
        // Low threats: Log and continue monitoring
        await TriggerSecurityAlert(threat, AlertLevel.Low);

        Log.LowThreatResponse(_logger, threat.Id, threat.ClientId);
    }

    // Threat response implementations (simplified for demo)
    private async Task BlockClientImmediately(string clientId)
    {
        await Task.Delay(50);
        Log.ClientBlocked(_logger, clientId);
    }

    private async Task ApplyRateLimiting(string clientId, RateLimitLevel level)
    {
        await Task.Delay(20);
        Log.RateLimitApplied(_logger, clientId, level.ToString());
    }

    private async Task TriggerSecurityAlert(DetectedThreat threat, AlertLevel level)
    {
        await Task.Delay(10);
        Log.SecurityAlertTriggered(_logger, threat.Type.ToString(), level.ToString());
    }

    private async Task NotifySecurityTeam(DetectedThreat threat)
    {
        await Task.Delay(30);
        Log.SecurityTeamNotified(_logger, threat.Id);
    }

    private async Task EnhanceMonitoring(string clientId)
    {
        await Task.Delay(15);
        Log.MonitoringEnhanced(_logger, clientId);
    }

    private void UpdateClientProfile(SecurityEvent securityEvent)
    {
        if (!_clientProfiles.TryGetValue(securityEvent.ClientId, out var profile))
        {
            profile = new ClientThreatProfile
            {
                ClientId = securityEvent.ClientId,
                FirstSeen = DateTime.UtcNow,
                Events = []
            };
            _clientProfiles[securityEvent.ClientId] = profile;
        }

        profile.LastSeen = DateTime.UtcNow;
        profile.Events.Add(securityEvent);

        // Keep only recent events to manage memory
        if (profile.Events.Count > 1000)
        {
            profile.Events = profile.Events
                .Where(e => e.Timestamp > DateTime.UtcNow.AddHours(-24))
                .ToList();
        }
    }

    private async Task AnalyzeClientBehaviorPatterns()
    {
        var suspiciousClients = new List<string>();

        foreach (var profile in _clientProfiles.Values)
        {
            if (await IsClientBehaviorSuspicious(profile))
            {
                suspiciousClients.Add(profile.ClientId);
            }
        }

        if (suspiciousClients.Count > 0)
        {
            Log.SuspiciousClientsDetected(_logger, suspiciousClients.Count);
        }
    }

    private async Task<bool> IsClientBehaviorSuspicious(ClientThreatProfile profile)
    {
        await Task.Delay(5);

        var recentEvents = profile.Events
            .Where(e => e.Timestamp > DateTime.UtcNow.AddHours(-1))
            .ToList();

        // Check for unusual activity patterns
        var eventRate = recentEvents.Count;
        var errorRate = recentEvents.Count(e => e.Severity >= SecurityEventSeverity.Medium) / Math.Max(recentEvents.Count, 1.0);

        return eventRate > 500 || errorRate > 0.5;
    }

    private async Task UpdateThreatLevel()
    {
        var currentThreatLevel = await CalculateCurrentThreatLevel();
        _activeThreatLevel.Record(currentThreatLevel);

        Log.ThreatLevelUpdated(_logger, currentThreatLevel);
    }

    private async Task<long> CalculateCurrentThreatLevel()
    {
        await Task.Delay(10);

        var recentThreats = _threatCounts.Values.Sum();
        var activeSessions = _clientProfiles.Count;

        if (recentThreats == 0) return 1; // Minimal threat
        if (recentThreats < 10) return 2; // Low threat
        if (recentThreats < 50) return 3; // Medium threat
        if (recentThreats < 100) return 4; // High threat
        return 5; // Critical threat level
    }

    public void RecordSecurityEvent(string source, string clientId, string data,
        SecurityEventSeverity severity = SecurityEventSeverity.Low)
    {
        var securityEvent = new SecurityEvent
        {
            Id = Guid.NewGuid().ToString(),
            Source = source,
            ClientId = clientId,
            Data = data,
            Severity = severity,
            Timestamp = DateTime.UtcNow
        };

        _eventQueue.Enqueue(securityEvent);
    }

    public ThreatDetectionStatus GetThreatStatus()
    {
        var currentThreatLevel = CalculateCurrentThreatLevelSync();

        return new ThreatDetectionStatus
        {
            LastAnalysis = _lastAnalysis,
            CurrentThreatLevel = currentThreatLevel,
            TotalThreatsDetected = _threatCounts.Values.Sum(),
            ActiveClientProfiles = _clientProfiles.Count,
            ThreatCountsByType = new Dictionary<ThreatType, int>(_threatCounts),
            QueuedEvents = _eventQueue.Count
        };
    }

    private long CalculateCurrentThreatLevelSync()
    {
        var recentThreats = _threatCounts.Values.Sum();
        var activeSessions = _clientProfiles.Count;
        var queueBacklog = _eventQueue.Count;

        // Calculate threat level based on multiple factors
        var threatScore = 0;

        // Recent threat count factor (40% weight)
        threatScore += recentThreats switch
        {
            0 => 0,
            < 10 => 1,
            < 50 => 2,
            < 100 => 3,
            _ => 4
        };

        // Active sessions factor (30% weight)
        threatScore += activeSessions switch
        {
            0 => 0,
            < 50 => 0,
            < 200 => 1,
            < 500 => 2,
            _ => 3
        };

        // Queue backlog factor (30% weight)
        threatScore += queueBacklog switch
        {
            0 => 0,
            < 100 => 0,
            < 500 => 1,
            < 1000 => 2,
            _ => 3
        };

        // Convert score to threat level (1-5)
        return threatScore switch
        {
            0 => 1, // Minimal
            <= 2 => 2, // Low
            <= 5 => 3, // Medium
            <= 8 => 4, // High
            _ => 5 // Critical
        };
    }

    public void Dispose()
    {
        _analysisTimer?.Dispose();
        _meter?.Dispose();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 9201, Level = LogLevel.Information,
            Message = "Threat detection service initialized ({PatternCount} threat patterns)")]
        public static partial void ThreatDetectionServiceInitialized(ILogger logger, int patternCount);

        [LoggerMessage(EventId = 9202, Level = LogLevel.Information,
            Message = "Threat detection service started")]
        public static partial void ThreatDetectionServiceStarted(ILogger logger);

        [LoggerMessage(EventId = 9203, Level = LogLevel.Information,
            Message = "Threat detection service stopped")]
        public static partial void ThreatDetectionServiceStopped(ILogger logger);

        [LoggerMessage(EventId = 9204, Level = LogLevel.Debug,
            Message = "Threat analysis completed: {EventCount} events in {DurationSeconds}s")]
        public static partial void ThreatAnalysisCompleted(ILogger logger, int eventCount, double durationSeconds);

        [LoggerMessage(EventId = 9205, Level = LogLevel.Error,
            Message = "Threat analysis failed")]
        public static partial void ThreatAnalysisFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9206, Level = LogLevel.Warning,
            Message = "Threat detected: {ThreatType} ({Severity}) from {ClientId} (confidence: {Confidence:F2})")]
        public static partial void ThreatDetected(ILogger logger, string threatType, string severity,
            string clientId, double confidence);

        [LoggerMessage(EventId = 9207, Level = LogLevel.Information,
            Message = "Threat handled: {ThreatId} ({ThreatType}) from {ClientId} (confidence: {Confidence:F2})")]
        public static partial void ThreatHandled(ILogger logger, string threatId, string threatType,
            string clientId, double confidence);

        [LoggerMessage(EventId = 9208, Level = LogLevel.Critical,
            Message = "Critical threat response executed: {ThreatId} from {ClientId}")]
        public static partial void CriticalThreatResponse(ILogger logger, string threatId, string clientId);

        [LoggerMessage(EventId = 9209, Level = LogLevel.Warning,
            Message = "High threat response executed: {ThreatId} from {ClientId}")]
        public static partial void HighThreatResponse(ILogger logger, string threatId, string clientId);

        [LoggerMessage(EventId = 9210, Level = LogLevel.Information,
            Message = "Medium threat response executed: {ThreatId} from {ClientId}")]
        public static partial void MediumThreatResponse(ILogger logger, string threatId, string clientId);

        [LoggerMessage(EventId = 9211, Level = LogLevel.Debug,
            Message = "Low threat response executed: {ThreatId} from {ClientId}")]
        public static partial void LowThreatResponse(ILogger logger, string threatId, string clientId);

        [LoggerMessage(EventId = 9212, Level = LogLevel.Critical,
            Message = "Client blocked: {ClientId}")]
        public static partial void ClientBlocked(ILogger logger, string clientId);

        [LoggerMessage(EventId = 9213, Level = LogLevel.Warning,
            Message = "Rate limit applied to {ClientId}: {Level}")]
        public static partial void RateLimitApplied(ILogger logger, string clientId, string level);

        [LoggerMessage(EventId = 9214, Level = LogLevel.Information,
            Message = "Security alert triggered: {ThreatType} ({Level})")]
        public static partial void SecurityAlertTriggered(ILogger logger, string threatType, string level);

        [LoggerMessage(EventId = 9215, Level = LogLevel.Critical,
            Message = "Security team notified for threat: {ThreatId}")]
        public static partial void SecurityTeamNotified(ILogger logger, string threatId);

        [LoggerMessage(EventId = 9216, Level = LogLevel.Information,
            Message = "Enhanced monitoring enabled for client: {ClientId}")]
        public static partial void MonitoringEnhanced(ILogger logger, string clientId);

        [LoggerMessage(EventId = 9217, Level = LogLevel.Information,
            Message = "Suspicious clients detected: {Count}")]
        public static partial void SuspiciousClientsDetected(ILogger logger, int count);

        [LoggerMessage(EventId = 9218, Level = LogLevel.Debug,
            Message = "Threat level updated: {Level}")]
        public static partial void ThreatLevelUpdated(ILogger logger, long level);

        [LoggerMessage(EventId = 9219, Level = LogLevel.Warning,
            Message = "Threat pattern analysis timeout: {PatternId}")]
        public static partial void ThreatPatternTimeout(ILogger logger, string patternId);

        [LoggerMessage(EventId = 9220, Level = LogLevel.Error,
            Message = "Threat pattern analysis failed: {PatternId}")]
        public static partial void ThreatPatternAnalysisFailed(ILogger logger, string patternId, Exception exception);

        [LoggerMessage(EventId = 9221, Level = LogLevel.Error,
            Message = "Security event processing failed: {EventId}")]
        public static partial void SecurityEventProcessingFailed(ILogger logger, string eventId, Exception exception);
    }
}

// Supporting types
public sealed class ThreatDetectionOptions
{
    public TimeSpan AnalysisInterval { get; set; } = TimeSpan.FromSeconds(30);
    public int BruteForceThreshold { get; set; } = 5;
    public int DdosThreshold { get; set; } = 100;
    public bool EnableBehavioralAnalysis { get; set; } = true;
    public bool EnableRealTimeBlocking { get; set; } = true;
}

public sealed class ThreatPattern
{
    public string Id { get; set; } = string.Empty;
    public ThreatType Type { get; set; }
    public ThreatSeverity Severity { get; set; }
    public List<string> Patterns { get; set; } = [];
    public string Description { get; set; } = string.Empty;
}

public sealed class DetectedThreat
{
    public string Id { get; set; } = string.Empty;
    public ThreatType Type { get; set; }
    public ThreatSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public string SourceData { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public sealed class SecurityEvent
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public SecurityEventSeverity Severity { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed class ClientThreatProfile
{
    public string ClientId { get; set; } = string.Empty;
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public List<SecurityEvent> Events { get; set; } = [];
}

public sealed class ThreatDetectionStatus
{
    public DateTime LastAnalysis { get; set; }
    public long CurrentThreatLevel { get; set; }
    public int TotalThreatsDetected { get; set; }
    public int ActiveClientProfiles { get; set; }
    public Dictionary<ThreatType, int> ThreatCountsByType { get; set; } = [];
    public int QueuedEvents { get; set; }
}

public enum ThreatType
{
    SqlInjection,
    CrossSiteScripting,
    BruteForce,
    DirectoryTraversal,
    CommandInjection,
    DenialOfService,
    AnomalousActivity
}

public enum ThreatSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}


public enum AlertLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum RateLimitLevel
{
    None,
    Moderate,
    Strict,
    Block
}