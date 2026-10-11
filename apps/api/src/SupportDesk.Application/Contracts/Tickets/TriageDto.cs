namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Human-readable reasons explaining the automated triage decisions for a ticket.
/// </summary>
public sealed record TriageDto(
    string PriorityReason,
    string SlaReason,
    string AssignmentReason);
