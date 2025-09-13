using System.Diagnostics.CodeAnalysis;

// Minimal suppressions for analyzer warnings that would otherwise prevent build
[assembly: SuppressMessage("Performance", "CA1515:Consider making public types internal",
    Justification = "FastEndpoints classes need to be discoverable by framework")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types",
    Justification = "Health check endpoints need broad exception handling")]
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "ConfigureAwait(false) not required for async using statements")]