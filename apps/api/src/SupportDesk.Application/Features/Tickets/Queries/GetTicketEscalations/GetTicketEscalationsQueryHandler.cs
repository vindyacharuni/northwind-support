using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

/// <summary>
/// Returns the escalation history of a ticket, newest first.
/// </summary>
public sealed class GetTicketEscalationsQueryHandler(
    ITicketRepository tickets,
    ITicketQueries ticketQueries)
{
    /// <exception cref="NotFoundException">Ticket does not exist.</exception>
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(int ticketId, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(ticketId, ct);
        if (ticket is null)
        {
            throw new NotFoundException("Ticket", ticketId);
        }

        return await ticketQueries.GetEscalationsAsync(ticketId, ct);
    }
}
