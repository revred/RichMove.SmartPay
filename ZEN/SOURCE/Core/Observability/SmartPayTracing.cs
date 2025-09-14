using System.Diagnostics;

namespace RichMove.SmartPay.Core.Observability
{
    /// <summary>
    /// Single ActivitySource for distributed tracing across the platform.
    /// </summary>
    public static class SmartPayTracing
    {
        public static readonly ActivitySource Source = new("RichMove.SmartPay");
    }
}