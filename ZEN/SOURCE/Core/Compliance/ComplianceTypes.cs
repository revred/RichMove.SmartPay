namespace RichMove.SmartPay.Core.Compliance
{
    /// <summary>
    /// Canonical compliance frameworks. Keep 'None = 0' to satisfy CA1008.
    /// </summary>
    public enum ComplianceFramework
    {
        None = 0,
        PCIDSS,
        GDPR,
        SOX,
        HIPAA,
        ISO27001
    }

    /// <summary>
    /// Standardized severity for compliance violations.
    /// </summary>
    public enum ComplianceSeverity
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    /// <summary>
    /// State of a given compliance check/run.
    /// </summary>
    public enum ComplianceState
    {
        Unknown = 0,
        Passing = 1,
        Warning = 2,
        Failing = 3
    }
}