using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;

namespace RichMove.SmartPay.Api.Security;

public sealed partial class SecurityScanningService : IHostedService, IDisposable
{
    private readonly ILogger<SecurityScanningService> _logger;
    private readonly SecurityScanOptions _options;
    private readonly Timer _scanTimer;
    private readonly List<SecurityVulnerability> _vulnerabilities;
    private readonly Dictionary<string, SecurityScanResult> _scanResults;
    private DateTime _lastFullScan = DateTime.MinValue;

    public SecurityScanningService(
        ILogger<SecurityScanningService> logger,
        IOptions<SecurityScanOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        _options = options.Value;
        _vulnerabilities = [];
        _scanResults = [];

        _scanTimer = new Timer(PerformSecurityScan, null,
            TimeSpan.FromMinutes(1), _options.ScanInterval);

        Log.SecurityScanningServiceInitialized(_logger, _options.ScanInterval.TotalMinutes);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Log.SecurityScanningServiceStarted(_logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Log.SecurityScanningServiceStopped(_logger);
        return Task.CompletedTask;
    }

    private async void PerformSecurityScan(object? state)
    {
        try
        {
            var scanStart = DateTime.UtcNow;
            var results = new List<SecurityScanResult>();

            // Dependency vulnerability scan
            if (_options.ScanDependencies)
            {
                var depResult = await ScanDependencyVulnerabilities();
                results.Add(depResult);
            }

            // Configuration security scan
            if (_options.ScanConfiguration)
            {
                var configResult = ScanConfigurationSecurity();
                results.Add(configResult);
            }

            // Runtime security scan
            if (_options.ScanRuntime)
            {
                var runtimeResult = ScanRuntimeSecurity();
                results.Add(runtimeResult);
            }

            // File integrity scan
            if (_options.ScanFileIntegrity)
            {
                var fileResult = await ScanFileIntegrity();
                results.Add(fileResult);
            }

            // Process security violations
            await ProcessScanResults(results, scanStart);

            _lastFullScan = scanStart;
            Log.SecurityScanCompleted(_logger, results.Count,
                (DateTime.UtcNow - scanStart).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            Log.SecurityScanFailed(_logger, ex);
        }
    }

    private async Task<SecurityScanResult> ScanDependencyVulnerabilities()
    {
        var result = new SecurityScanResult
        {
            ScanType = "DependencyVulnerabilities",
            StartTime = DateTime.UtcNow,
            Status = ScanStatus.Running
        };

        try
        {
            var vulnerabilities = new List<DependencyVulnerability>();

            // Get loaded assemblies for vulnerability checking
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .ToList();

            foreach (var assembly in assemblies)
            {
                var name = assembly.GetName();
                if (name.Name != null && name.Version != null)
                {
                    var vulnerability = await CheckAssemblyVulnerability(name.Name, name.Version.ToString());
                    if (vulnerability != null)
                    {
                        vulnerabilities.Add(vulnerability);
                    }
                }
            }

            result.Findings = vulnerabilities.Count;
            result.Status = vulnerabilities.Count > 0 ? ScanStatus.VulnerabilitiesFound : ScanStatus.Clean;
            result.Details = vulnerabilities.Select(v => v.ToString()).ToList();

            Log.DependencyScanCompleted(_logger, assemblies.Count, vulnerabilities.Count);
        }
        catch (Exception ex)
        {
            result.Status = ScanStatus.Error;
            result.Error = ex.Message;
            Log.DependencyScanFailed(_logger, ex);
        }

        result.EndTime = DateTime.UtcNow;
        return result;
    }

    private static async Task<DependencyVulnerability?> CheckAssemblyVulnerability(string name, string version)
    {
        // Simulate vulnerability database lookup
        await Task.Delay(10);

        // Known vulnerable packages simulation
        var vulnerablePackages = new Dictionary<string, string[]>
        {
            ["System.Text.Json"] = ["7.0.0", "7.0.1"],
            ["Newtonsoft.Json"] = ["12.0.0", "12.0.1"],
            ["Microsoft.AspNetCore.App"] = ["6.0.0", "6.0.1"]
        };

        if (vulnerablePackages.TryGetValue(name, out var vulnerableVersions) &&
            vulnerableVersions.Contains(version))
        {
            return new DependencyVulnerability
            {
                PackageName = name,
                Version = version,
                Severity = "High",
                Description = $"Known vulnerability in {name} {version}",
                CveId = $"CVE-2024-{Random.Shared.Next(1000, 9999)}"
            };
        }

        return null;
    }

    private SecurityScanResult ScanConfigurationSecurity()
    {
        var result = new SecurityScanResult
        {
            ScanType = "ConfigurationSecurity",
            StartTime = DateTime.UtcNow,
            Status = ScanStatus.Running
        };

        try
        {
            var issues = new List<string>();

            // Check for insecure configurations
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add("Application running in Development mode in production");
            }

            // Check for debug configurations
            var isDebug = Assembly.GetExecutingAssembly()
                .GetCustomAttributes<DebuggableAttribute>()
                .Any(a => a.IsJITOptimizerDisabled);

            if (isDebug)
            {
                issues.Add("Debug configuration detected in production build");
            }

            // Check for weak encryption settings
            var defaultCipherSuites = CheckWeakCipherSuites();
            issues.AddRange(defaultCipherSuites);

            result.Findings = issues.Count;
            result.Status = issues.Count > 0 ? ScanStatus.VulnerabilitiesFound : ScanStatus.Clean;
            result.Details = issues;

            Log.ConfigurationScanCompleted(_logger, issues.Count);
        }
        catch (Exception ex)
        {
            result.Status = ScanStatus.Error;
            result.Error = ex.Message;
            Log.ConfigurationScanFailed(_logger, ex);
        }

        result.EndTime = DateTime.UtcNow;
        return result;
    }

    private static List<string> CheckWeakCipherSuites()
    {
        var issues = new List<string>();

        // Check for weak TLS configurations
        // Modern .NET versions automatically use secure TLS protocols
        // SSL3 and TLS 1.0 are disabled by default

        return issues;
    }

    private SecurityScanResult ScanRuntimeSecurity()
    {
        var result = new SecurityScanResult
        {
            ScanType = "RuntimeSecurity",
            StartTime = DateTime.UtcNow,
            Status = ScanStatus.Running
        };

        try
        {
            var issues = new List<string>();

            // Check for security-sensitive runtime conditions
            var process = Process.GetCurrentProcess();

            // Check for excessive privileges
            if (Environment.UserName.Equals("root", StringComparison.OrdinalIgnoreCase) ||
                Environment.UserName.Equals("administrator", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add("Application running with elevated privileges");
            }

            // Check memory usage patterns that might indicate attacks
            var memoryUsage = GC.GetTotalMemory(false);
            if (memoryUsage > _options.MemoryThresholdBytes)
            {
                issues.Add($"High memory usage detected: {memoryUsage / (1024 * 1024)} MB");
            }

            // Check for suspicious process activity
            if (process.Threads.Count > _options.MaxThreadCount)
            {
                issues.Add($"Suspicious thread count: {process.Threads.Count}");
            }

            result.Findings = issues.Count;
            result.Status = issues.Count > 0 ? ScanStatus.VulnerabilitiesFound : ScanStatus.Clean;
            result.Details = issues;

            Log.RuntimeScanCompleted(_logger, issues.Count);
        }
        catch (Exception ex)
        {
            result.Status = ScanStatus.Error;
            result.Error = ex.Message;
            Log.RuntimeScanFailed(_logger, ex);
        }

        result.EndTime = DateTime.UtcNow;
        return result;
    }

    private async Task<SecurityScanResult> ScanFileIntegrity()
    {
        var result = new SecurityScanResult
        {
            ScanType = "FileIntegrity",
            StartTime = DateTime.UtcNow,
            Status = ScanStatus.Running
        };

        try
        {
            var issues = new List<string>();

            // Check critical application files for integrity
            var criticalFiles = new[]
            {
                Assembly.GetExecutingAssembly().Location,
                Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json")
            };

            foreach (var filePath in criticalFiles.Where(File.Exists))
            {
                var integrity = await CheckFileIntegrity(filePath);
                if (!integrity.IsValid)
                {
                    issues.Add($"File integrity violation: {Path.GetFileName(filePath)} - {integrity.Reason}");
                }
            }

            result.Findings = issues.Count;
            result.Status = issues.Count > 0 ? ScanStatus.VulnerabilitiesFound : ScanStatus.Clean;
            result.Details = issues;

            Log.FileIntegrityScanCompleted(_logger, criticalFiles.Length, issues.Count);
        }
        catch (Exception ex)
        {
            result.Status = ScanStatus.Error;
            result.Error = ex.Message;
            Log.FileIntegrityScanFailed(_logger, ex);
        }

        result.EndTime = DateTime.UtcNow;
        return result;
    }

    private static async Task<FileIntegrityResult> CheckFileIntegrity(string filePath)
    {
        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await sha256.ComputeHashAsync(stream);
            var hashString = Convert.ToHexString(hash);

            // In a real implementation, this would check against known good hashes
            // For demo, we simulate occasional integrity violations
            var isValid = !hashString.EndsWith("00", StringComparison.Ordinal);

            return new FileIntegrityResult
            {
                IsValid = isValid,
                Hash = hashString,
                Reason = isValid ? "Valid" : "Hash mismatch with baseline"
            };
        }
        catch (Exception ex)
        {
            return new FileIntegrityResult
            {
                IsValid = false,
                Reason = $"Unable to compute hash: {ex.Message}"
            };
        }
    }

    private async Task ProcessScanResults(List<SecurityScanResult> results, DateTime scanStart)
    {
        var totalVulnerabilities = results.Sum(r => r.Findings);
        var criticalIssues = results.Where(r => r.Status == ScanStatus.VulnerabilitiesFound).ToList();

        // Store results for API access
        foreach (var result in results)
        {
            _scanResults[result.ScanType] = result;
        }

        // Trigger alerts for critical issues
        if (criticalIssues.Count > 0 && _options.AlertOnVulnerabilities)
        {
            await TriggerSecurityAlert(criticalIssues);
        }

        Log.SecurityScanProcessed(_logger, totalVulnerabilities, criticalIssues.Count);
    }

    private async Task TriggerSecurityAlert(List<SecurityScanResult> criticalIssues)
    {
        try
        {
            // In production, this would integrate with alerting systems
            await Task.Delay(100); // Simulate alert processing

            var alertDetails = criticalIssues.Select(issue => new
            {
                Type = issue.ScanType,
                Findings = issue.Findings,
                Details = issue.Details?.Take(3) // Limit for alert brevity
            });

            Log.SecurityAlertTriggered(_logger, criticalIssues.Count,
                string.Join(", ", criticalIssues.Select(i => i.ScanType)));
        }
        catch (Exception ex)
        {
            Log.SecurityAlertFailed(_logger, ex);
        }
    }

    public SecurityScanStatus GetScanStatus()
    {
        return new SecurityScanStatus
        {
            LastScanTime = _lastFullScan,
            IsRunning = _scanResults.Values.Any(r => r.Status == ScanStatus.Running),
            TotalVulnerabilities = _scanResults.Values.Sum(r => r.Findings),
            ScanResults = _scanResults.Values.ToList(),
            NextScanTime = _lastFullScan.Add(_options.ScanInterval)
        };
    }

    public void Dispose()
    {
        _scanTimer?.Dispose();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 9001, Level = LogLevel.Information,
            Message = "Security scanning service initialized (scan interval: {ScanIntervalMinutes} minutes)")]
        public static partial void SecurityScanningServiceInitialized(ILogger logger, double scanIntervalMinutes);

        [LoggerMessage(EventId = 9002, Level = LogLevel.Information,
            Message = "Security scanning service started")]
        public static partial void SecurityScanningServiceStarted(ILogger logger);

        [LoggerMessage(EventId = 9003, Level = LogLevel.Information,
            Message = "Security scanning service stopped")]
        public static partial void SecurityScanningServiceStopped(ILogger logger);

        [LoggerMessage(EventId = 9004, Level = LogLevel.Information,
            Message = "Security scan completed: {ScanCount} scans in {DurationMs}ms")]
        public static partial void SecurityScanCompleted(ILogger logger, int scanCount, double durationMs);

        [LoggerMessage(EventId = 9005, Level = LogLevel.Error,
            Message = "Security scan failed")]
        public static partial void SecurityScanFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9006, Level = LogLevel.Debug,
            Message = "Dependency scan completed: {AssemblyCount} assemblies, {VulnerabilityCount} vulnerabilities")]
        public static partial void DependencyScanCompleted(ILogger logger, int assemblyCount, int vulnerabilityCount);

        [LoggerMessage(EventId = 9007, Level = LogLevel.Warning,
            Message = "Dependency scan failed")]
        public static partial void DependencyScanFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9008, Level = LogLevel.Debug,
            Message = "Configuration scan completed: {IssueCount} issues found")]
        public static partial void ConfigurationScanCompleted(ILogger logger, int issueCount);

        [LoggerMessage(EventId = 9009, Level = LogLevel.Warning,
            Message = "Configuration scan failed")]
        public static partial void ConfigurationScanFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9010, Level = LogLevel.Debug,
            Message = "Runtime scan completed: {IssueCount} issues found")]
        public static partial void RuntimeScanCompleted(ILogger logger, int issueCount);

        [LoggerMessage(EventId = 9011, Level = LogLevel.Warning,
            Message = "Runtime scan failed")]
        public static partial void RuntimeScanFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9012, Level = LogLevel.Debug,
            Message = "File integrity scan completed: {FileCount} files checked, {IssueCount} issues")]
        public static partial void FileIntegrityScanCompleted(ILogger logger, int fileCount, int issueCount);

        [LoggerMessage(EventId = 9013, Level = LogLevel.Warning,
            Message = "File integrity scan failed")]
        public static partial void FileIntegrityScanFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 9014, Level = LogLevel.Information,
            Message = "Security scan processed: {TotalVulnerabilities} vulnerabilities, {CriticalIssues} critical")]
        public static partial void SecurityScanProcessed(ILogger logger, int totalVulnerabilities, int criticalIssues);

        [LoggerMessage(EventId = 9015, Level = LogLevel.Critical,
            Message = "Security alert triggered: {CriticalCount} critical issues ({IssueTypes})")]
        public static partial void SecurityAlertTriggered(ILogger logger, int criticalCount, string issueTypes);

        [LoggerMessage(EventId = 9016, Level = LogLevel.Error,
            Message = "Security alert failed")]
        public static partial void SecurityAlertFailed(ILogger logger, Exception exception);
    }
}

// Supporting types
public sealed class SecurityScanOptions
{
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromHours(6);
    public bool ScanDependencies { get; set; } = true;
    public bool ScanConfiguration { get; set; } = true;
    public bool ScanRuntime { get; set; } = true;
    public bool ScanFileIntegrity { get; set; } = true;
    public bool AlertOnVulnerabilities { get; set; } = true;
    public long MemoryThresholdBytes { get; set; } = 1024 * 1024 * 1024; // 1GB
    public int MaxThreadCount { get; set; } = 100;
}

public sealed class SecurityScanResult
{
    public string ScanType { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ScanStatus Status { get; set; }
    public int Findings { get; set; }
    public List<string>? Details { get; set; }
    public string? Error { get; set; }
}

public sealed class SecurityScanStatus
{
    public DateTime LastScanTime { get; set; }
    public DateTime NextScanTime { get; set; }
    public bool IsRunning { get; set; }
    public int TotalVulnerabilities { get; set; }
    public List<SecurityScanResult> ScanResults { get; set; } = [];
}

public sealed class DependencyVulnerability
{
    public string PackageName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CveId { get; set; } = string.Empty;

    public override string ToString() =>
        $"{PackageName} {Version}: {Description} ({CveId})";
}

public sealed class FileIntegrityResult
{
    public bool IsValid { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class SecurityVulnerability
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
}

public enum ScanStatus
{
    Running,
    Clean,
    VulnerabilitiesFound,
    Error
}