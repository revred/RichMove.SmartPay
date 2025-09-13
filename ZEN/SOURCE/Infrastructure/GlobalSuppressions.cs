using System.Diagnostics.CodeAnalysis;

// Suppress CA2007 for async using statements - ConfigureAwait(false) is not applicable
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "ConfigureAwait(false) not applicable to async using statements",
    Scope = "member", Target = "~M:RichMove.SmartPay.Infrastructure.ForeignExchange.SupabasePricingProvider.RefreshAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task")]

[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "ConfigureAwait(false) not applicable to async using statements",
    Scope = "member", Target = "~M:RichMove.SmartPay.Infrastructure.ForeignExchange.SupabaseFxRateSource.TryGet(System.String,System.Threading.CancellationToken)~System.Threading.Tasks.Task{System.Nullable{System.Decimal}}")]

[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "ConfigureAwait(false) not applicable to async using statements",
    Scope = "member", Target = "~M:RichMove.SmartPay.Infrastructure.ForeignExchange.SupabaseFxQuoteRepository.SaveAsync(RichMove.SmartPay.Core.ForeignExchange.FxQuoteResult,System.Nullable{System.Guid},System.Threading.CancellationToken)~System.Threading.Tasks.Task")]

// Suppress CA1848 for simple logging scenarios where performance is not critical
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates for improved performance",
    Justification = "Simple logging scenarios where performance is acceptable",
    Scope = "member", Target = "~M:RichMove.SmartPay.Infrastructure.Supabase.SupabasePricingRefreshService.ExecuteAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task")]

[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates for improved performance",
    Justification = "Simple logging scenarios where performance is acceptable",
    Scope = "member", Target = "~M:RichMove.SmartPay.Infrastructure.ForeignExchange.SimpleFxQuoteProvider.GetQuoteAsync(RichMove.SmartPay.Core.ForeignExchange.FxQuoteRequest,System.Threading.CancellationToken)~System.Threading.Tasks.Task{RichMove.SmartPay.Core.ForeignExchange.FxQuoteResult}")]

// Suppress CA1031 for background service error handling where broad exception catching is appropriate
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types",
    Justification = "Background service needs to handle all exceptions to prevent service crash",
    Scope = "member", Target = "~M:RichMove.SmartPay.Infrastructure.Supabase.SupabasePricingRefreshService.ExecuteAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task")]