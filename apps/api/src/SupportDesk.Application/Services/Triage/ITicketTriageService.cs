using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services.Triage;

public sealed record TriageDecision(
    TicketPriority Priority,
    DateTime? DueAtUtc,
    int? AssignedAgentId,
    TriageDto Triage,
    SlaStatus SlaStatus);

public sealed record AgentSelectionResult(
    int? SelectedAgentId,
    string Reason);

public interface ITicketTriageService
{
    TicketPriority DeterminePriority(Category category, TicketPriority? requestedPriority, out string reason);

    AgentSelectionResult SelectAgent(
        Category category,
        IReadOnlyList<Agent> agents,
        IReadOnlyDictionary<int, int> openTicketCounts,
        int? currentAgentId = null);

    TriageDecision TriageOnCreate(
        Category category,
        Customer customer,
        TicketPriority? requestedPriority,
        IReadOnlyList<Agent> candidateAgents,
        IReadOnlyDictionary<int, int> openTicketCounts,
        DateTime nowUtc);
}
