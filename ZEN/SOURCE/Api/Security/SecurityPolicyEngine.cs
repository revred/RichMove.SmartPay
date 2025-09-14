using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Diagnostics.Metrics;

namespace RichMove.SmartPay.Api.Security;

public sealed partial class SecurityPolicyEngine : IHostedService, IDisposable
{
    private readonly ILogger<SecurityPolicyEngine> _logger;
    private readonly SecurityPolicyOptions _options;
    private readonly Timer _policyTimer;
    private readonly Dictionary<string, SecurityPolicy> _policies;
    private readonly ConcurrentQueue<PolicyViolation> _violations;
    private readonly Dictionary<string, PolicyEnforcementResult> _enforcementResults;
    private readonly Meter _meter;

    // Policy metrics
    private readonly Counter<long> _policyViolations;
    private readonly Counter<long> _policyEvaluations;
    private readonly Gauge<long> _activePolicies;

    public SecurityPolicyEngine(
        ILogger<SecurityPolicyEngine> logger,
        IOptions<SecurityPolicyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        _options = options.Value;
        _policies = [];
        _violations = new ConcurrentQueue<PolicyViolation>();
        _enforcementResults = [];

        _meter = new Meter("richmove.smartpay.security.policies");

        _policyViolations = _meter.CreateCounter<long>(
            "richmove_smartpay_policy_violations_total",
            "violations",
            "Total number of security policy violations");

        _policyEvaluations = _meter.CreateCounter<long>(
            "richmove_smartpay_policy_evaluations_total",
            "evaluations",
            "Total number of policy evaluations");

        _activePolicies = _meter.CreateGauge<long>(
            "richmove_smartpay_active_policies",
            "policies",
            "Number of active security policies");

        InitializeDefaultPolicies();

        _policyTimer = new Timer(EvaluatePolicyCompliance, null,
            TimeSpan.FromMinutes(1), _options.EvaluationInterval);

        Log.SecurityPolicyEngineInitialized(_logger, _policies.Count);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Log.SecurityPolicyEngineStarted(_logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Log.SecurityPolicyEngineStopped(_logger);
        return Task.CompletedTask;
    }

    private void InitializeDefaultPolicies()
    {
        // Authentication Policy
        _policies["auth_policy"] = new SecurityPolicy
        {
            Id = "auth_policy",
            Name = "Authentication Security Policy",
            Type = PolicyType.Authentication,
            Severity = PolicySeverity.High,
            Rules = [
                new PolicyRule
                {
                    Id = "strong_passwords",
                    Description = "Passwords must meet complexity requirements",
                    Condition = "password_length >= 8 AND password_complexity >= 3",
                    Action = PolicyAction.Reject,
                    Enabled = true
                },
                new PolicyRule
                {
                    Id = "mfa_required",
                    Description = "Multi-factor authentication required for admin users",
                    Condition = "user_role == 'admin' AND mfa_enabled == false",
                    Action = PolicyAction.Warn,
                    Enabled = true
                }
            ],
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        // Data Access Policy
        _policies["data_access_policy"] = new SecurityPolicy
        {
            Id = "data_access_policy",
            Name = "Data Access Security Policy",
            Type = PolicyType.DataAccess,
            Severity = PolicySeverity.Critical,
            Rules = [
                new PolicyRule
                {
                    Id = "pii_access_logging",
                    Description = "All PII access must be logged",
                    Condition = "data_contains_pii == true",
                    Action = PolicyAction.Log,
                    Enabled = true
                },
                new PolicyRule
                {
                    Id = "encryption_required",
                    Description = "Sensitive data must be encrypted at rest",
                    Condition = "data_sensitivity >= 3 AND encryption_enabled == false",
                    Action = PolicyAction.Block,
                    Enabled = true
                }
            ],
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        // Network Security Policy
        _policies["network_policy"] = new SecurityPolicy
        {
            Id = "network_policy",
            Name = "Network Security Policy",
            Type = PolicyType.Network,
            Severity = PolicySeverity.Medium,
            Rules = [
                new PolicyRule
                {
                    Id = "https_required",
                    Description = "All external communications must use HTTPS",
                    Condition = "protocol == 'http' AND destination_external == true",
                    Action = PolicyAction.Block,
                    Enabled = true
                },
                new PolicyRule
                {
                    Id = "rate_limiting",
                    Description = "Rate limiting must be enforced for public endpoints",
                    Condition = "endpoint_public == true AND rate_limit_enabled == false",
                    Action = PolicyAction.Warn,
                    Enabled = true
                }
            ],
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        // Compliance Policy
        _policies["compliance_policy"] = new SecurityPolicy
        {
            Id = "compliance_policy",
            Name = "Regulatory Compliance Policy",
            Type = PolicyType.Compliance,
            Severity = PolicySeverity.Critical,
            Rules = [
                new PolicyRule
                {
                    Id = "gdpr_consent",
                    Description = "GDPR consent required for data processing",
                    Condition = "data_subject_eu == true AND consent_obtained == false",
                    Action = PolicyAction.Block,
                    Enabled = true
                },
                new PolicyRule
                {
                    Id = "audit_trail",
                    Description = "Audit trail required for sensitive operations",
                    Condition = "operation_sensitivity >= 3 AND audit_enabled == false",
                    Action = PolicyAction.Reject,
                    Enabled = true
                }
            ],
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        // API Security Policy
        _policies["api_security_policy"] = new SecurityPolicy
        {
            Id = "api_security_policy",
            Name = "API Security Policy",
            Type = PolicyType.ApiSecurity,
            Severity = PolicySeverity.High,
            Rules = [
                new PolicyRule
                {
                    Id = "api_key_required",
                    Description = "API key required for all external API calls",
                    Condition = "source_internal == false AND api_key_present == false",
                    Action = PolicyAction.Block,
                    Enabled = true
                },
                new PolicyRule
                {
                    Id = "input_validation",
                    Description = "Input validation required for all endpoints",
                    Condition = "input_validated == false",
                    Action = PolicyAction.Warn,
                    Enabled = true
                }
            ],
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        _activePolicies.Record(_policies.Count);
    }

    private async void EvaluatePolicyCompliance(object? state)
    {
        try
        {
            var evaluationStart = DateTime.UtcNow;
            var evaluationsPerformed = 0;

            foreach (var policy in _policies.Values.Where(p => p.Enabled))
            {
                var result = await EvaluatePolicy(policy);
                _enforcementResults[policy.Id] = result;

                if (result.HasViolations)
                {
                    await HandlePolicyViolations(policy, result.Violations);
                }

                evaluationsPerformed++;
                _policyEvaluations.Add(1,
                    new KeyValuePair<string, object?>("policy_id", policy.Id),
                    new KeyValuePair<string, object?>("policy_type", policy.Type.ToString()));
            }

            Log.PolicyComplianceEvaluated(_logger, evaluationsPerformed,
                (DateTime.UtcNow - evaluationStart).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            Log.PolicyEvaluationFailed(_logger, ex);
        }
    }

    private async Task<PolicyEnforcementResult> EvaluatePolicy(SecurityPolicy policy)
    {
        var result = new PolicyEnforcementResult
        {
            PolicyId = policy.Id,
            EvaluatedAt = DateTime.UtcNow,
            Violations = []
        };

        try
        {
            foreach (var rule in policy.Rules.Where(r => r.Enabled))
            {
                var ruleViolation = await EvaluateRule(policy, rule);
                if (ruleViolation != null)
                {
                    result.Violations.Add(ruleViolation);
                }
            }

            result.IsCompliant = result.Violations.Count == 0;
            result.Status = result.IsCompliant ? "Compliant" : "NonCompliant";

            Log.PolicyEvaluated(_logger, policy.Id, result.Violations.Count);
        }
        catch (Exception ex)
        {
            result.Status = "Error";
            result.Error = ex.Message;
            Log.PolicyEvaluationError(_logger, policy.Id, ex);
        }

        return result;
    }

    private async Task<PolicyViolation?> EvaluateRule(SecurityPolicy policy, PolicyRule rule)
    {
        try
        {
            // Simulate rule evaluation based on current system state
            var isViolation = await EvaluateRuleCondition(rule);

            if (isViolation)
            {
                var violation = new PolicyViolation
                {
                    Id = Guid.NewGuid().ToString(),
                    PolicyId = policy.Id,
                    RuleId = rule.Id,
                    Description = rule.Description,
                    Severity = policy.Severity,
                    Action = rule.Action,
                    DetectedAt = DateTime.UtcNow,
                    Context = await GatherViolationContext(rule)
                };

                _violations.Enqueue(violation);
                _policyViolations.Add(1,
                    new KeyValuePair<string, object?>("policy_id", policy.Id),
                    new KeyValuePair<string, object?>("rule_id", rule.Id),
                    new KeyValuePair<string, object?>("action", rule.Action.ToString()));

                return violation;
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.RuleEvaluationFailed(_logger, rule.Id, ex);
            return null;
        }
    }

    private async Task<bool> EvaluateRuleCondition(PolicyRule rule)
    {
        // Simulate rule condition evaluation
        await Task.Delay(Random.Shared.Next(10, 50));

        return rule.Id switch
        {
            "strong_passwords" => Random.Shared.Next(100) < 5, // 5% violation rate
            "mfa_required" => Random.Shared.Next(100) < 10, // 10% violation rate
            "pii_access_logging" => Random.Shared.Next(100) < 2, // 2% violation rate
            "encryption_required" => Random.Shared.Next(100) < 3, // 3% violation rate
            "https_required" => Random.Shared.Next(100) < 1, // 1% violation rate
            "rate_limiting" => Random.Shared.Next(100) < 15, // 15% violation rate
            "gdpr_consent" => Random.Shared.Next(100) < 5, // 5% violation rate
            "audit_trail" => Random.Shared.Next(100) < 8, // 8% violation rate
            "api_key_required" => Random.Shared.Next(100) < 12, // 12% violation rate
            "input_validation" => Random.Shared.Next(100) < 20, // 20% violation rate
            _ => false
        };
    }

    private async Task<Dictionary<string, object>> GatherViolationContext(PolicyRule rule)
    {
        await Task.Delay(10);

        return new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow,
            ["rule_id"] = rule.Id,
            ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            ["machine_name"] = Environment.MachineName,
            ["user_agent"] = "SmartPay/1.0",
            ["correlation_id"] = Guid.NewGuid().ToString()
        };
    }

    private async Task HandlePolicyViolations(SecurityPolicy policy, List<PolicyViolation> violations)
    {
        foreach (var violation in violations)
        {
            await ExecutePolicyAction(violation);
            Log.PolicyViolationHandled(_logger, violation.Id, violation.PolicyId,
                violation.RuleId, violation.Action.ToString());
        }
    }

    private async Task ExecutePolicyAction(PolicyViolation violation)
    {
        switch (violation.Action)
        {
            case PolicyAction.Block:
                await ExecuteBlockAction(violation);
                break;

            case PolicyAction.Reject:
                await ExecuteRejectAction(violation);
                break;

            case PolicyAction.Warn:
                await ExecuteWarnAction(violation);
                break;

            case PolicyAction.Log:
                await ExecuteLogAction(violation);
                break;

            case PolicyAction.Alert:
                await ExecuteAlertAction(violation);
                break;
        }
    }

    private async Task ExecuteBlockAction(PolicyViolation violation)
    {
        await Task.Delay(20);
        Log.PolicyActionExecuted(_logger, "Block", violation.Id);
    }

    private async Task ExecuteRejectAction(PolicyViolation violation)
    {
        await Task.Delay(15);
        Log.PolicyActionExecuted(_logger, "Reject", violation.Id);
    }

    private async Task ExecuteWarnAction(PolicyViolation violation)
    {
        await Task.Delay(10);
        Log.PolicyActionExecuted(_logger, "Warn", violation.Id);
    }

    private async Task ExecuteLogAction(PolicyViolation violation)
    {
        await Task.Delay(5);
        Log.PolicyActionExecuted(_logger, "Log", violation.Id);
    }

    private async Task ExecuteAlertAction(PolicyViolation violation)
    {
        await Task.Delay(25);
        Log.PolicyActionExecuted(_logger, "Alert", violation.Id);
    }

    public PolicyEnforcementSummary GetEnforcementSummary()
    {
        var violationsList = new List<PolicyViolation>();
        while (_violations.TryDequeue(out var violation))
        {
            violationsList.Add(violation);
        }

        var recentViolations = violationsList
            .Where(v => v.DetectedAt > DateTime.UtcNow.AddHours(-24))
            .ToList();

        return new PolicyEnforcementSummary
        {
            GeneratedAt = DateTime.UtcNow,
            TotalPolicies = _policies.Count,
            ActivePolicies = _policies.Values.Count(p => p.Enabled),
            TotalViolations = recentViolations.Count,
            CriticalViolations = recentViolations.Count(v => v.Severity == PolicySeverity.Critical),
            ViolationsByType = recentViolations
                .GroupBy(v => _policies[v.PolicyId].Type)
                .ToDictionary(g => g.Key.ToString(), g => g.Count()),
            ViolationsByAction = recentViolations
                .GroupBy(v => v.Action)
                .ToDictionary(g => g.Key.ToString(), g => g.Count()),
            RecentViolations = recentViolations
                .OrderByDescending(v => v.DetectedAt)
                .Take(10)
                .ToList()
        };
    }

    public List<SecurityPolicy> GetActivePolicies()
    {
        return _policies.Values.Where(p => p.Enabled).ToList();
    }

    public SecurityPolicy? GetPolicy(string policyId)
    {
        return _policies.TryGetValue(policyId, out var policy) ? policy : null;
    }

    public void AddPolicy(SecurityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        _policies[policy.Id] = policy;
        _activePolicies.Record(_policies.Count);

        Log.PolicyAdded(_logger, policy.Id, policy.Name);
    }

    public bool RemovePolicy(string policyId)
    {
        if (_policies.Remove(policyId))
        {
            _activePolicies.Record(_policies.Count);
            Log.PolicyRemoved(_logger, policyId);
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        _policyTimer?.Dispose();
        _meter?.Dispose();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 9301, Level = LogLevel.Information,
            Message = "Security policy engine initialized ({PolicyCount} policies)")]
        public static partial void SecurityPolicyEngineInitialized(ILogger logger, int policyCount);

        [LoggerMessage(EventId = 9302, Level = LogLevel.Information,
            Message = "Security policy engine started")]
        public static partial void SecurityPolicyEngineStarted(ILogger logger);

        [LoggerMessage(EventId = 9303, Level = LogLevel.Information,
            Message = "Security policy engine stopped")]
        public static partial void SecurityPolicyEngineStopped(ILogger logger);

        [LoggerMessage(EventId = 9304, Level = LogLevel.Debug,
            Message = "Policy compliance evaluated: {EvaluationCount} evaluations in {DurationMs}ms")]
        public static partial void PolicyComplianceEvaluated(ILogger logger, int evaluationCount, double durationMs);

        [LoggerMessage(EventId = 9305, Level = LogLevel.Error,
            Message = "Policy evaluation failed")]
        public static partial void PolicyEvaluationFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9306, Level = LogLevel.Debug,
            Message = "Policy evaluated: {PolicyId} ({ViolationCount} violations)")]
        public static partial void PolicyEvaluated(ILogger logger, string policyId, int violationCount);

        [LoggerMessage(EventId = 9307, Level = LogLevel.Error,
            Message = "Policy evaluation error: {PolicyId}")]
        public static partial void PolicyEvaluationError(ILogger logger, string policyId, Exception exception);

        [LoggerMessage(EventId = 9308, Level = LogLevel.Error,
            Message = "Rule evaluation failed: {RuleId}")]
        public static partial void RuleEvaluationFailed(ILogger logger, string ruleId, Exception exception);

        [LoggerMessage(EventId = 9309, Level = LogLevel.Warning,
            Message = "Policy violation handled: {ViolationId} ({PolicyId}.{RuleId}) -> {Action}")]
        public static partial void PolicyViolationHandled(ILogger logger, string violationId,
            string policyId, string ruleId, string action);

        [LoggerMessage(EventId = 9310, Level = LogLevel.Information,
            Message = "Policy action executed: {Action} for violation {ViolationId}")]
        public static partial void PolicyActionExecuted(ILogger logger, string action, string violationId);

        [LoggerMessage(EventId = 9311, Level = LogLevel.Information,
            Message = "Policy added: {PolicyId} ({PolicyName})")]
        public static partial void PolicyAdded(ILogger logger, string policyId, string policyName);

        [LoggerMessage(EventId = 9312, Level = LogLevel.Information,
            Message = "Policy removed: {PolicyId}")]
        public static partial void PolicyRemoved(ILogger logger, string policyId);
    }
}

// Supporting types
public sealed class SecurityPolicyOptions
{
    public TimeSpan EvaluationInterval { get; set; } = TimeSpan.FromMinutes(5);
    public bool EnableRealTimeEnforcement { get; set; } = true;
    public bool EnablePolicyAuditing { get; set; } = true;
}

public sealed class SecurityPolicy
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PolicyType Type { get; set; }
    public PolicySeverity Severity { get; set; }
    public List<PolicyRule> Rules { get; set; } = [];
    public bool Enabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class PolicyRule
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public PolicyAction Action { get; set; }
    public bool Enabled { get; set; }
}

public sealed class PolicyViolation
{
    public string Id { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PolicySeverity Severity { get; set; }
    public PolicyAction Action { get; set; }
    public DateTime DetectedAt { get; set; }
    public Dictionary<string, object> Context { get; set; } = [];
}

public sealed class PolicyEnforcementResult
{
    public string PolicyId { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; }
    public bool IsCompliant { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<PolicyViolation> Violations { get; set; } = [];
    public string? Error { get; set; }
    public bool HasViolations => Violations.Count > 0;
}

public sealed class PolicyEnforcementSummary
{
    public DateTime GeneratedAt { get; set; }
    public int TotalPolicies { get; set; }
    public int ActivePolicies { get; set; }
    public int TotalViolations { get; set; }
    public int CriticalViolations { get; set; }
    public Dictionary<string, int> ViolationsByType { get; set; } = [];
    public Dictionary<string, int> ViolationsByAction { get; set; } = [];
    public List<PolicyViolation> RecentViolations { get; set; } = [];
}

public enum PolicyType
{
    Authentication,
    DataAccess,
    Network,
    Compliance,
    ApiSecurity
}

public enum PolicySeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum PolicyAction
{
    Log,
    Warn,
    Alert,
    Reject,
    Block
}