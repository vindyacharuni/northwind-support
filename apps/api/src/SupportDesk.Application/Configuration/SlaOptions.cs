namespace SupportDesk.Application.Configuration;

/// <summary>
/// Configuration options for SLA window calculations and thresholds.
/// </summary>
public sealed class SlaOptions
{
    public const string SectionName = "Sla";

    public Dictionary<string, double> BaseWindowHours { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Low"] = 72,
        ["Medium"] = 24,
        ["High"] = 8,
        ["Critical"] = 4
    };

    public double PremiumCustomerMultiplier { get; set; } = 0.5;

    public double MinimumWindowHours { get; set; } = 1.0;

    public double AtRiskThresholdPercent { get; set; } = 25.0;
}
