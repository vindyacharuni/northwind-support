using SupportDesk.Application.Configuration;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services.Sla;

public sealed class SlaPolicy(SlaOptions? options = null) : ISlaPolicy
{
    private readonly SlaOptions _options = options ?? new SlaOptions();

    public double AtRiskThresholdPercent => _options.AtRiskThresholdPercent;

    public TimeSpan CalculateWindow(TicketPriority priority, CustomerTier customerTier)
    {
        var priorityKey = priority.ToString();
        var baseHours = _options.BaseWindowHours.TryGetValue(priorityKey, out var configuredHours)
            ? configuredHours
            : priority switch
            {
                TicketPriority.Critical => 4,
                TicketPriority.High => 8,
                TicketPriority.Medium => 24,
                TicketPriority.Low => 72,
                _ => 24
            };

        var multiplier = customerTier == CustomerTier.Premium ? _options.PremiumCustomerMultiplier : 1.0;
        var windowHours = baseHours * multiplier;

        if (windowHours < _options.MinimumWindowHours)
        {
            windowHours = _options.MinimumWindowHours;
        }

        return TimeSpan.FromHours(windowHours);
    }

    public DateTime CalculateDueAt(DateTime fromTimeUtc, TicketPriority priority, CustomerTier customerTier)
    {
        var window = CalculateWindow(priority, customerTier);
        return fromTimeUtc.Add(window);
    }

    public SlaStatus EvaluateStatus(DateTime? dueAtUtc, DateTime? resolvedAtUtc, DateTime nowUtc, DateTime? createdAtUtc = null)
    {
        return SlaEvaluator.Evaluate(dueAtUtc, resolvedAtUtc, nowUtc, createdAtUtc, _options.AtRiskThresholdPercent);
    }
}
