using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services.Sla;

/// <summary>
/// Policy governing SLA window calculations and status evaluations.
/// </summary>
public interface ISlaPolicy
{
    TimeSpan CalculateWindow(TicketPriority priority, CustomerTier customerTier);

    DateTime CalculateDueAt(DateTime fromTimeUtc, TicketPriority priority, CustomerTier customerTier);

    SlaStatus EvaluateStatus(DateTime? dueAtUtc, DateTime? resolvedAtUtc, DateTime nowUtc, DateTime? createdAtUtc = null);

    double AtRiskThresholdPercent { get; }
}
