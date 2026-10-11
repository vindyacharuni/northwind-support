using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// A recorded escalation event on a ticket.
/// </summary>
public sealed class TicketEscalationDto
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    public TicketPriority FromPriority { get; set; }

    public TicketPriority ToPriority { get; set; }

    public int? FromAgentId { get; set; }

    public AgentSummaryDto? FromAgent { get; set; }

    public int? ToAgentId { get; set; }

    public AgentSummaryDto? ToAgent { get; set; }

    public DateTime? FromDueAtUtc { get; set; }

    public DateTime? ToDueAtUtc { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string EscalatedBy { get; set; } = string.Empty;

    public DateTime EscalatedAtUtc { get; set; }
}
