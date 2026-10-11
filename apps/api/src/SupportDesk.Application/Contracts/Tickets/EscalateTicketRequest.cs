namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Body of POST /api/tickets/{id}/escalate.
/// </summary>
public sealed record EscalateTicketRequest(
    string Reason,
    string? EscalatedBy);
