using SupportDesk.Application.Services.Sla;
using SupportDesk.Application.Services.Triage;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Services.Triage;

public class TicketTriageServiceTests
{
    private readonly TicketTriageService _service = new(new SlaPolicy());
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void DeterminePriority_WhenCategoryForcesCritical_ReturnsCritical()
    {
        var category = new Category("Security", requiresSpecialist: true, forcesCriticalPriority: true);

        var priority = _service.DeterminePriority(category, TicketPriority.Low, out var reason);

        Assert.Equal(TicketPriority.Critical, priority);
        Assert.Contains("forces Critical", reason);
    }

    [Fact]
    public void DeterminePriority_WhenCategoryDoesNotForceCritical_UsesRequestedPriority()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);

        var priority = _service.DeterminePriority(category, TicketPriority.High, out var reason);

        Assert.Equal(TicketPriority.High, priority);
        Assert.Contains("High", reason);
    }

    [Fact]
    public void DeterminePriority_WhenRequestedPriorityIsNull_DefaultsToMedium()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);

        var priority = _service.DeterminePriority(category, null, out var reason);

        Assert.Equal(TicketPriority.Medium, priority);
        Assert.Contains("Medium", reason);
    }

    [Fact]
    public void SelectAgent_ChoosesEligibleAgentWithFewestOpenTickets()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);

        var agent1 = new Agent("Agent One", "agent1@example.com", maxOpenTickets: 10, Now);
        var agent2 = new Agent("Agent Two", "agent2@example.com", maxOpenTickets: 10, Now);
        SetAgentId(agent1, 1);
        SetAgentId(agent2, 2);

        var counts = new Dictionary<int, int>
        {
            [1] = 5,
            [2] = 2
        };

        var result = _service.SelectAgent(category, new[] { agent1, agent2 }, counts);

        Assert.Equal(2, result.SelectedAgentId);
        Assert.Contains("Agent Two", result.Reason);
    }

    [Fact]
    public void SelectAgent_WhenCategoryRequiresSpecialist_OnlySelectsSpecialist()
    {
        var category = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        SetCategoryId(category, 2);

        var generalist = new Agent("Generalist", "gen@example.com", maxOpenTickets: 10, Now);
        SetAgentId(generalist, 1);

        var specialist = new Agent("Specialist", "spec@example.com", maxOpenTickets: 10, Now);
        specialist.AddSpecialization(2);
        SetAgentId(specialist, 2);

        var counts = new Dictionary<int, int>
        {
            [1] = 0, // Generalist has fewer tickets
            [2] = 3  // Specialist has 3 tickets
        };

        var result = _service.SelectAgent(category, new[] { generalist, specialist }, counts);

        Assert.Equal(2, result.SelectedAgentId);
    }

    [Fact]
    public void SelectAgent_WhenAgentIsAtLimit_ExcludesAgent()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);

        var fullAgent = new Agent("Full Agent", "full@example.com", maxOpenTickets: 5, Now);
        SetAgentId(fullAgent, 1);

        var counts = new Dictionary<int, int> { [1] = 5 }; // At max limit!

        var result = _service.SelectAgent(category, new[] { fullAgent }, counts);

        Assert.Null(result.SelectedAgentId);
        Assert.Contains("limit", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectAgent_WhenAgentIsInactive_ExcludesAgent()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);

        var inactiveAgent = new Agent("Inactive", "inactive@example.com", maxOpenTickets: 10, Now);
        inactiveAgent.Deactivate();
        SetAgentId(inactiveAgent, 1);

        var counts = new Dictionary<int, int> { [1] = 0 };

        var result = _service.SelectAgent(category, new[] { inactiveAgent }, counts);

        Assert.Null(result.SelectedAgentId);
    }

    [Fact]
    public void SelectAgent_WhenTieInOpenTickets_BreaksDeterministicallyById()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);

        var agentA = new Agent("Agent B", "b@example.com", maxOpenTickets: 10, Now);
        SetAgentId(agentA, 10);

        var agentB = new Agent("Agent A", "a@example.com", maxOpenTickets: 10, Now);
        SetAgentId(agentB, 5); // Smaller ID

        var counts = new Dictionary<int, int>
        {
            [10] = 3,
            [5] = 3
        };

        var result = _service.SelectAgent(category, new[] { agentA, agentB }, counts);

        Assert.Equal(5, result.SelectedAgentId);
    }

    [Fact]
    public void TriageOnCreate_ProducesComprehensiveDecision()
    {
        var category = new Category("Security", requiresSpecialist: true, forcesCriticalPriority: true);
        SetCategoryId(category, 5);

        var customer = new Customer("Contoso", "contoso@example.com", null, CustomerTier.Premium, Now);
        var specialist = new Agent("Priya", "priya@example.com", maxOpenTickets: 8, Now);
        specialist.AddSpecialization(5);
        SetAgentId(specialist, 3);

        var counts = new Dictionary<int, int> { [3] = 4 };

        var decision = _service.TriageOnCreate(category, customer, TicketPriority.Low, new[] { specialist }, counts, Now);

        Assert.Equal(TicketPriority.Critical, decision.Priority);
        Assert.Equal(3, decision.AssignedAgentId);
        // Critical base 4h, halved for Premium = 2h due window
        Assert.Equal(Now.AddHours(2), decision.DueAtUtc);
        Assert.NotNull(decision.Triage);
        Assert.Contains("forces Critical", decision.Triage.PriorityReason);
        Assert.Contains("Premium", decision.Triage.SlaReason);
    }

    private static void SetAgentId(Agent agent, int id) =>
        typeof(Agent).BaseType!.GetProperty("Id")!.SetValue(agent, id);

    private static void SetCategoryId(Category category, int id) =>
        typeof(Category).BaseType!.GetProperty("Id")!.SetValue(category, id);
}
