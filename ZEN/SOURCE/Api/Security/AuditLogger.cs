using RichMove.SmartPay.Core.Time;
using System.Text.Json;

namespace RichMove.SmartPay.Api.Security;

/// <summary>
/// Comprehensive audit trail for sensitive operations
/// Provides immutable security event logging with structured data
/// </summary>
public sealed partial class AuditLogger
{
    private readonly IClock _clock;
    private readonly ILogger<AuditLogger> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AuditLogger(IClock clock, ILogger<AuditLogger> logger)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Log authentication event
    /// </summary>
    public void LogAuthentication(string clientId, string endpoint, bool successful, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(endpoint);

        var auditEvent = new AuthenticationAuditEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = _clock.UtcNow,
            ClientId = clientId,
            Endpoint = endpoint,
            Successful = successful,
            Reason = reason
        };

        if (successful)
        {
            Log.AuthenticationSuccessful(_logger, auditEvent.EventId, clientId, endpoint, auditEvent.Timestamp);
        }
        else
        {
            Log.AuthenticationFailed(_logger, auditEvent.EventId, clientId, endpoint, reason ?? "Unknown", auditEvent.Timestamp);
        }

        LogStructuredEvent("Authentication", auditEvent);
    }

    /// <summary>
    /// Log authorization event
    /// </summary>
    public void LogAuthorization(string clientId, string resource, string action, bool granted, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(action);

        var auditEvent = new AuthorizationAuditEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = _clock.UtcNow,
            ClientId = clientId,
            Resource = resource,
            Action = action,
            Granted = granted,
            Reason = reason
        };

        if (granted)
        {
            Log.AuthorizationGranted(_logger, auditEvent.EventId, clientId, action, resource, auditEvent.Timestamp);
        }
        else
        {
            Log.AuthorizationDenied(_logger, auditEvent.EventId, clientId, action, resource, reason ?? "Insufficient privileges", auditEvent.Timestamp);
        }

        LogStructuredEvent("Authorization", auditEvent);
    }

    /// <summary>
    /// Log data access event
    /// </summary>
    public void LogDataAccess(string clientId, string dataType, string operation, int recordCount, bool successful)
    {
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(dataType);
        ArgumentNullException.ThrowIfNull(operation);

        var auditEvent = new DataAccessAuditEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = _clock.UtcNow,
            ClientId = clientId,
            DataType = dataType,
            Operation = operation,
            RecordCount = recordCount,
            Successful = successful
        };

        Log.DataAccess(_logger, auditEvent.EventId, clientId, operation, dataType, recordCount, successful, auditEvent.Timestamp);
        LogStructuredEvent("DataAccess", auditEvent);
    }

    /// <summary>
    /// Log security event (rate limiting, suspicious activity, etc.)
    /// </summary>
    public void LogSecurityEvent(string eventType, string clientId, string description, SecurityEventSeverity severity = SecurityEventSeverity.Medium)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(description);

        var auditEvent = new SecurityAuditEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = _clock.UtcNow,
            EventType = eventType,
            ClientId = clientId,
            Description = description,
            Severity = severity
        };

        var logLevel = severity switch
        {
            SecurityEventSeverity.Low => LogLevel.Information,
            SecurityEventSeverity.Medium => LogLevel.Warning,
            SecurityEventSeverity.High => LogLevel.Error,
            SecurityEventSeverity.Critical => LogLevel.Critical,
            _ => LogLevel.Warning
        };

        Log.SecurityEvent(_logger, logLevel, auditEvent.EventId, eventType, clientId, description, severity.ToString(), auditEvent.Timestamp);
        LogStructuredEvent("Security", auditEvent);
    }

    /// <summary>
    /// Log configuration change event
    /// </summary>
    public void LogConfigurationChange(string clientId, string setting, string? oldValue, string newValue)
    {
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(newValue);

        var auditEvent = new ConfigurationAuditEvent
        {
            EventId = Guid.NewGuid().ToString(),
            Timestamp = _clock.UtcNow,
            ClientId = clientId,
            Setting = setting,
            OldValue = oldValue,
            NewValue = newValue
        };

        Log.ConfigurationChange(_logger, auditEvent.EventId, clientId, setting, oldValue ?? "<null>", newValue, auditEvent.Timestamp);
        LogStructuredEvent("Configuration", auditEvent);
    }

    private void LogStructuredEvent<T>(string eventCategory, T auditEvent) where T : class
    {
        var json = JsonSerializer.Serialize(auditEvent, JsonOptions);

        Log.StructuredAuditEvent(_logger, eventCategory, json);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 5501, Level = LogLevel.Information, Message = "Authentication successful - Event {EventId}, Client {ClientId}, Endpoint {Endpoint}, Timestamp {Timestamp:O}")]
        public static partial void AuthenticationSuccessful(ILogger logger, string eventId, string clientId, string endpoint, DateTime timestamp);

        [LoggerMessage(EventId = 5502, Level = LogLevel.Warning, Message = "Authentication failed - Event {EventId}, Client {ClientId}, Endpoint {Endpoint}, Reason {Reason}, Timestamp {Timestamp:O}")]
        public static partial void AuthenticationFailed(ILogger logger, string eventId, string clientId, string endpoint, string reason, DateTime timestamp);

        [LoggerMessage(EventId = 5503, Level = LogLevel.Information, Message = "Authorization granted - Event {EventId}, Client {ClientId}, Action {Action}, Resource {Resource}, Timestamp {Timestamp:O}")]
        public static partial void AuthorizationGranted(ILogger logger, string eventId, string clientId, string action, string resource, DateTime timestamp);

        [LoggerMessage(EventId = 5504, Level = LogLevel.Warning, Message = "Authorization denied - Event {EventId}, Client {ClientId}, Action {Action}, Resource {Resource}, Reason {Reason}, Timestamp {Timestamp:O}")]
        public static partial void AuthorizationDenied(ILogger logger, string eventId, string clientId, string action, string resource, string reason, DateTime timestamp);

        [LoggerMessage(EventId = 5505, Level = LogLevel.Information, Message = "Data access - Event {EventId}, Client {ClientId}, Operation {Operation}, DataType {DataType}, RecordCount {RecordCount}, Successful {Successful}, Timestamp {Timestamp:O}")]
        public static partial void DataAccess(ILogger logger, string eventId, string clientId, string operation, string dataType, int recordCount, bool successful, DateTime timestamp);

        [LoggerMessage(EventId = 5506, Message = "Security event - Event {EventId}, Type {EventType}, Client {ClientId}, Description {Description}, Severity {Severity}, Timestamp {Timestamp:O}")]
        public static partial void SecurityEvent(ILogger logger, LogLevel level, string eventId, string eventType, string clientId, string description, string severity, DateTime timestamp);

        [LoggerMessage(EventId = 5507, Level = LogLevel.Information, Message = "Configuration change - Event {EventId}, Client {ClientId}, Setting {Setting}, OldValue {OldValue}, NewValue {NewValue}, Timestamp {Timestamp:O}")]
        public static partial void ConfigurationChange(ILogger logger, string eventId, string clientId, string setting, string oldValue, string newValue, DateTime timestamp);

        [LoggerMessage(EventId = 5508, Level = LogLevel.Information, Message = "AUDIT-{Category}: {StructuredData}")]
        public static partial void StructuredAuditEvent(ILogger logger, string category, string structuredData);
    }
}

/// <summary>
/// Base audit event
/// </summary>
public abstract class AuditEvent
{
    public required string EventId { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string ClientId { get; init; }
}

/// <summary>
/// Authentication audit event
/// </summary>
public sealed class AuthenticationAuditEvent : AuditEvent
{
    public required string Endpoint { get; init; }
    public required bool Successful { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Authorization audit event
/// </summary>
public sealed class AuthorizationAuditEvent : AuditEvent
{
    public required string Resource { get; init; }
    public required string Action { get; init; }
    public required bool Granted { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Data access audit event
/// </summary>
public sealed class DataAccessAuditEvent : AuditEvent
{
    public required string DataType { get; init; }
    public required string Operation { get; init; }
    public required int RecordCount { get; init; }
    public required bool Successful { get; init; }
}

/// <summary>
/// Security audit event
/// </summary>
public sealed class SecurityAuditEvent : AuditEvent
{
    public required string EventType { get; init; }
    public required string Description { get; init; }
    public required SecurityEventSeverity Severity { get; init; }
}

/// <summary>
/// Configuration change audit event
/// </summary>
public sealed class ConfigurationAuditEvent : AuditEvent
{
    public required string Setting { get; init; }
    public string? OldValue { get; init; }
    public required string NewValue { get; init; }
}

/// <summary>
/// Security event severity levels
/// </summary>
public enum SecurityEventSeverity
{
    Low,
    Medium,
    High,
    Critical
}