using System;

namespace RichMove.SmartPay.Core.Compliance
{
    /// <summary>
    /// Canonical compliance frameworks used across the platform.
    /// Centralized to eliminate duplicate enum declarations and enforce CA1008 (None = 0).
    /// </summary>
    public enum ComplianceFramework
    {
        None = 0,
        PCIDSS,
        GDPR,
        SOX,
        HIPAA,
        ISO27001,
        NIST
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
        Compliant = 1,
        NonCompliant = 2,
        Error = 3
    }
}