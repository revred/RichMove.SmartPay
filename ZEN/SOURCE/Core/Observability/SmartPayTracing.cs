using System.Diagnostics;

namespace RichMove.SmartPay.Core.Observability
{
    /// <summary>
    /// Central ActivitySource for RichMove.SmartPay tracing.
    /// </summary>
    public static class SmartPayTracing
    {
        public static readonly ActivitySource Source = new("RichMove.SmartPay");
    }
}