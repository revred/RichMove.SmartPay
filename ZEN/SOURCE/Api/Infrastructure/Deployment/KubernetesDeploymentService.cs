using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace RichMove.SmartPay.Api.Infrastructure.Deployment;

public sealed partial class KubernetesDeploymentService : IDisposable
{
    private readonly ILogger<KubernetesDeploymentService> _logger;
    private readonly DeploymentOptions _options;
    private readonly Timer _statusTimer;
    private readonly ActivitySource _activitySource;

    public KubernetesDeploymentService(
        ILogger<KubernetesDeploymentService> logger,
        IOptions<DeploymentOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        _options = options.Value;
        _activitySource = new ActivitySource("richmove.smartpay.deployment");

        _statusTimer = new Timer(ReportDeploymentStatus, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        Log.DeploymentServiceInitialized(_logger, Environment.MachineName);
    }

    public async Task<DeploymentInfo> GetDeploymentInfoAsync(CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("get-deployment-info");

        var info = new DeploymentInfo
        {
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            Version = GetApplicationVersion(),
            BuildTimestamp = GetBuildTimestamp(),
            ContainerInfo = await GetContainerInfoAsync(cancellationToken),
            KubernetesInfo = await GetKubernetesInfoAsync(cancellationToken)
        };

        activity?.SetTag("deployment.environment", info.Environment);
        activity?.SetTag("deployment.version", info.Version);

        Log.DeploymentInfoRetrieved(_logger, info.Environment, info.Version);
        return info;
    }

    private static string GetApplicationVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "0.0.0.0";
    }

    private static DateTime GetBuildTimestamp()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var attribute = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildTimestamp");

        if (attribute?.Value != null && DateTime.TryParse(attribute.Value, out var timestamp))
            return timestamp;

        return File.GetCreationTimeUtc(assembly.Location);
    }

    private static async Task<ContainerInfo?> GetContainerInfoAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Check if running in container
            if (!File.Exists("/.dockerenv") && !File.Exists("/proc/1/cgroup"))
                return null;

            var containerInfo = new ContainerInfo
            {
                IsContainer = true,
                Runtime = DetectContainerRuntime(),
                ImageId = Environment.GetEnvironmentVariable("IMAGE_ID"),
                ImageTag = Environment.GetEnvironmentVariable("IMAGE_TAG")
            };

            // Get container ID from cgroup
            if (File.Exists("/proc/self/cgroup"))
            {
                var cgroup = await File.ReadAllTextAsync("/proc/self/cgroup", cancellationToken);
                containerInfo.ContainerId = ExtractContainerIdFromCgroup(cgroup);
            }

            return containerInfo;
        }
        catch (Exception)
        {
            return new ContainerInfo { IsContainer = false };
        }
    }

    private static string DetectContainerRuntime()
    {
        if (Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") != null)
            return "kubernetes";
        if (Environment.GetEnvironmentVariable("DOCKER_CONTAINER") != null)
            return "docker";
        return "unknown";
    }

    private static string? ExtractContainerIdFromCgroup(string cgroup)
    {
        var lines = cgroup.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("docker", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('/');
                var dockerPart = Array.FindLast(parts, p => p.Length == 64);
                if (dockerPart != null)
                    return dockerPart[..12]; // Return short container ID
            }
        }
        return null;
    }

    private static async Task<KubernetesInfo?> GetKubernetesInfoAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") == null)
                return null;

            var k8sInfo = new KubernetesInfo
            {
                IsKubernetes = true,
                Namespace = await ReadKubernetesFileAsync("/var/run/secrets/kubernetes.io/serviceaccount/namespace", cancellationToken),
                ServiceAccount = Environment.GetEnvironmentVariable("SERVICE_ACCOUNT_NAME"),
                PodName = Environment.GetEnvironmentVariable("HOSTNAME"),
                NodeName = Environment.GetEnvironmentVariable("NODE_NAME")
            };

            return k8sInfo;
        }
        catch (Exception)
        {
            return new KubernetesInfo { IsKubernetes = false };
        }
    }

    private static async Task<string?> ReadKubernetesFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            return (await File.ReadAllTextAsync(path, cancellationToken)).Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void ReportDeploymentStatus(object? state)
    {
        try
        {
            var memoryUsage = GC.GetTotalMemory(false) / (1024 * 1024);
            var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();

            Log.DeploymentStatusReport(_logger, memoryUsage, uptime.TotalSeconds);
        }
        catch (Exception ex)
        {
            Log.DeploymentStatusReportFailed(_logger, ex);
        }
    }

    public void Dispose()
    {
        _statusTimer?.Dispose();
        _activitySource?.Dispose();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 8501, Level = LogLevel.Information,
            Message = "Kubernetes deployment service initialized on {MachineName}")]
        public static partial void DeploymentServiceInitialized(ILogger logger, string machineName);

        [LoggerMessage(EventId = 8502, Level = LogLevel.Debug,
            Message = "Deployment info retrieved: {Environment} v{Version}")]
        public static partial void DeploymentInfoRetrieved(ILogger logger, string environment, string version);

        [LoggerMessage(EventId = 8503, Level = LogLevel.Debug,
            Message = "Deployment status: {MemoryMB}MB memory, {UptimeSeconds}s uptime")]
        public static partial void DeploymentStatusReport(ILogger logger, long memoryMB, double uptimeSeconds);

        [LoggerMessage(EventId = 8504, Level = LogLevel.Warning,
            Message = "Failed to report deployment status")]
        public static partial void DeploymentStatusReportFailed(ILogger logger, Exception exception);
    }
}

public sealed class DeploymentOptions
{
    public bool EnableStatusReporting { get; set; } = true;
    public TimeSpan StatusReportInterval { get; set; } = TimeSpan.FromSeconds(30);
}

public sealed class DeploymentInfo
{
    public string Environment { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime BuildTimestamp { get; set; }
    public ContainerInfo? ContainerInfo { get; set; }
    public KubernetesInfo? KubernetesInfo { get; set; }
}

public sealed class ContainerInfo
{
    public bool IsContainer { get; set; }
    public string Runtime { get; set; } = string.Empty;
    public string? ContainerId { get; set; }
    public string? ImageId { get; set; }
    public string? ImageTag { get; set; }
}

public sealed class KubernetesInfo
{
    public bool IsKubernetes { get; set; }
    public string? Namespace { get; set; }
    public string? ServiceAccount { get; set; }
    public string? PodName { get; set; }
    public string? NodeName { get; set; }
}