using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Services.Sla;
using SupportDesk.Application.Services.Triage;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Escalates a ticket to the next priority level, recalculating the SLA due date from now
/// and re-evaluating the assigned agent.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    ITicketTriageService triageService,
    ISlaPolicy slaPolicy,
    ITicketQueries ticketQueries,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">Ticket, customer, or category not found.</exception>
    /// <exception cref="BusinessRuleViolationException">Ticket is critical or resolved/closed.</exception>
    public async Task<EscalateTicketResponse> HandleAsync(int id, EscalateTicketRequest request, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct);
        if (ticket is null)
        {
            throw new NotFoundException("Ticket", id);
        }

        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {ticket.Reference} is already at Critical priority and cannot be escalated.");
        }

        if (ticket.Status == TicketStatus.Resolved)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {ticket.Reference} is Resolved and cannot be escalated.");
        }

        if (ticket.Status == TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {ticket.Reference} is Closed and cannot be escalated.");
        }

        var nextPriority = ticket.Priority switch
        {
            TicketPriority.Low => TicketPriority.Medium,
            TicketPriority.Medium => TicketPriority.High,
            TicketPriority.High => TicketPriority.Critical,
            _ => throw new BusinessRuleViolationException($"Ticket {ticket.Reference} cannot be escalated from {ticket.Priority}.")
        };

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct);
        if (customer is null)
        {
            throw new NotFoundException("Customer", ticket.CustomerId);
        }

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct);
        if (category is null)
        {
            throw new NotFoundException("Category", ticket.CategoryId);
        }

        var now = clock.UtcNow;
        var newDueAtUtc = slaPolicy.CalculateDueAt(now, nextPriority, customer.Tier);

        var activeAgents = await agents.GetAllActiveAsync(ct);
        var openTicketCounts = await tickets.GetOpenTicketCountsByAgentAsync(ct);

        var selection = triageService.SelectAgent(category, activeAgents, openTicketCounts, ticket.AssignedAgentId);
        var newAgentId = selection.SelectedAgentId;

        var escalation = ticket.Escalate(
            nextPriority,
            newAgentId,
            newDueAtUtc,
            request.Reason,
            request.EscalatedBy ?? string.Empty,
            now);

        await tickets.AddEscalationAsync(escalation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {FromPriority} to {ToPriority} by {EscalatedBy}. Agent: {AgentId}, Due: {DueAt}",
            ticket.Reference, escalation.FromPriority, escalation.ToPriority, escalation.EscalatedBy, ticket.AssignedAgentId, ticket.DueAtUtc);

        var updatedTicket = await getTicket.HandleAsync(ticket.Id, ct);
        var escalations = await ticketQueries.GetEscalationsAsync(ticket.Id, ct);
        var recordedEscalation = escalations.FirstOrDefault() ?? new TicketEscalationDto
        {
            Id = escalation.Id,
            TicketId = escalation.TicketId,
            FromPriority = escalation.FromPriority,
            ToPriority = escalation.ToPriority,
            FromAgentId = escalation.FromAgentId,
            ToAgentId = escalation.ToAgentId,
            FromDueAtUtc = escalation.FromDueAtUtc,
            ToDueAtUtc = escalation.ToDueAtUtc,
            Reason = escalation.Reason,
            EscalatedBy = escalation.EscalatedBy,
            EscalatedAtUtc = escalation.EscalatedAtUtc
        };

        return new EscalateTicketResponse(updatedTicket, recordedEscalation);
    }
}
