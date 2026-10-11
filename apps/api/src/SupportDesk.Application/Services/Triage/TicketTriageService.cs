using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Services.Sla;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services.Triage;

public sealed class TicketTriageService(ISlaPolicy slaPolicy) : ITicketTriageService
{
    public TicketPriority DeterminePriority(Category category, TicketPriority? requestedPriority, out string reason)
    {
        if (category.ForcesCriticalPriority)
        {
            reason = $"Category '{category.Name}' forces Critical priority.";
            return TicketPriority.Critical;
        }

        if (requestedPriority is not null)
        {
            reason = $"Requested priority '{requestedPriority.Value}' applied.";
            return requestedPriority.Value;
        }

        reason = "Default priority Medium applied.";
        return TicketPriority.Medium;
    }

    public AgentSelectionResult SelectAgent(
        Category category,
        IReadOnlyList<Agent> agents,
        IReadOnlyDictionary<int, int> openTicketCounts,
        int? currentAgentId = null)
    {
        // For escalation: if current agent is still eligible, keep them.
        if (currentAgentId is not null)
        {
            var currentAgent = agents.FirstOrDefault(a => a.Id == currentAgentId.Value);
            if (currentAgent is not null && currentAgent.IsActive)
            {
                var isQualified = !category.RequiresSpecialist ||
                    currentAgent.Specializations.Any(s => s.CategoryId == category.Id);

                var openCount = openTicketCounts.GetValueOrDefault(currentAgent.Id, 0);
                // Current agent already holds this ticket, so remaining at or under max is valid.
                var withinCapacity = openCount <= currentAgent.MaxOpenTickets;

                if (isQualified && withinCapacity)
                {
                    return new AgentSelectionResult(
                        currentAgent.Id,
                        $"Current agent {currentAgent.FullName} is still eligible and retained.");
                }
            }
        }

        // BR-4 & BR-5: Select among eligible agents (active, specialized where required, strictly below MaxOpenTickets).
        var eligibleCandidates = agents
            .Where(a => a.IsActive)
            .Where(a => !category.RequiresSpecialist || a.Specializations.Any(s => s.CategoryId == category.Id))
            .Where(a => openTicketCounts.GetValueOrDefault(a.Id, 0) < a.MaxOpenTickets)
            .OrderBy(a => openTicketCounts.GetValueOrDefault(a.Id, 0))
            .ThenBy(a => a.Id)
            .ToList();

        if (eligibleCandidates.Count == 0)
        {
            string unassignedReason;
            if (category.RequiresSpecialist)
            {
                var activeSpecialists = agents
                    .Where(a => a.IsActive && a.Specializations.Any(s => s.CategoryId == category.Id))
                    .ToList();

                unassignedReason = activeSpecialists.Count == 0
                    ? $"No active specialist available for '{category.Name}' category."
                    : $"All specialists for '{category.Name}' are at capacity.";
            }
            else
            {
                unassignedReason = "No eligible agents are currently below their open ticket limit.";
            }

            return new AgentSelectionResult(null, unassignedReason);
        }

        var chosenAgent = eligibleCandidates[0];
        var chosenOpenCount = openTicketCounts.GetValueOrDefault(chosenAgent.Id, 0);

        return new AgentSelectionResult(
            chosenAgent.Id,
            $"Assigned to {chosenAgent.FullName} ({chosenOpenCount} open tickets).");
    }

    public TriageDecision TriageOnCreate(
        Category category,
        Customer customer,
        TicketPriority? requestedPriority,
        IReadOnlyList<Agent> candidateAgents,
        IReadOnlyDictionary<int, int> openTicketCounts,
        DateTime nowUtc)
    {
        var priority = DeterminePriority(category, requestedPriority, out var priorityReason);

        var dueAtUtc = slaPolicy.CalculateDueAt(nowUtc, priority, customer.Tier);
        var window = slaPolicy.CalculateWindow(priority, customer.Tier);
        var slaReason = customer.Tier == CustomerTier.Premium
            ? $"Premium customer SLA window of {window.TotalHours:0.#}h applied (50% reduction)."
            : $"Standard SLA window of {window.TotalHours:0.#}h applied.";

        var assignment = SelectAgent(category, candidateAgents, openTicketCounts);

        var slaStatus = slaPolicy.EvaluateStatus(dueAtUtc, null, nowUtc, nowUtc);

        var triageDto = new TriageDto(
            priorityReason,
            slaReason,
            assignment.Reason);

        return new TriageDecision(
            priority,
            dueAtUtc,
            assignment.SelectedAgentId,
            triageDto,
            slaStatus);
    }
}
