namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Response of POST /api/tickets/{id}/escalate.
/// </summary>
public sealed record EscalateTicketResponse(
    TicketDetailDto Ticket,
    TicketEscalationDto Escalation);
