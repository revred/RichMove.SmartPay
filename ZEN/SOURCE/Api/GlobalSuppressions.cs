using System.Diagnostics.CodeAnalysis;

// Suppress CA1812 for FastEndpoints - they are instantiated by the framework via DI
[assembly: SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "FastEndpoints are instantiated by the framework",
    Scope = "type", Target = "~T:RichMove.SmartPay.Api.Endpoints.Health.DbEndpoint")]

// Suppress CA2007 for async using statements - ConfigureAwait(false) is not applicable
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "ConfigureAwait(false) not applicable to async using statements",
    Scope = "member", Target = "~M:RichMove.SmartPay.Api.Endpoints.Health.DbEndpoint.HandleAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task")]