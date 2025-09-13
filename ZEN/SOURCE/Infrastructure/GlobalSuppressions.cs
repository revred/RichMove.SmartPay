using System.Diagnostics.CodeAnalysis;

// Minimal suppressions for analyzer warnings that would otherwise prevent build
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "ConfigureAwait(false) not required for async using statements")]
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates for improved performance",
    Justification = "Simple logging scenarios where performance is acceptable")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types",
    Justification = "Background service needs broad exception handling to prevent crashes")]