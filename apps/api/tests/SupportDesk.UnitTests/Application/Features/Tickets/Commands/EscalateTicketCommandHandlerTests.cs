using Moq;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private static EscalateTicketRequest Request(string reason = "Issue is escalating due to executive escalation") =>
        new(reason, "Alex Turner");

    [Fact]
    public async Task HandleAsync_RaisesPriorityByOneStepAndRecomputesDueFromNow()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Medium).Build());

        var response = await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        // Medium escalated to High: High window for Premium customer = 8h * 0.5 = 4h
        Assert.Equal(context.Clock.UtcNow.AddHours(4), ticket.DueAtUtc);
        context.VerifySaved(Times.Once());
        context.Tickets.Verify(t => t.AddEscalationAsync(It.IsAny<TicketEscalation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenCurrentAgentIsStillEligible_RetainsCurrentAgent()
    {
        var context = new TicketCommandTestContext();

        var agent = new Agent("Sara Lindqvist", "sara@example.com", maxOpenTickets: 6, context.Clock.UtcNow);
        SetId(agent, 4);
        context.Agents.Setup(a => a.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { agent });

        var ticket = context.ExistingTicket(
            new TicketBuilder()
                .WithPriority(TicketPriority.Low)
                .AssignedTo(4)
                .Build());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(4, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenCurrentAgentIsInactive_ReassignsToEligibleAgent()
    {
        var context = new TicketCommandTestContext();

        var inactiveAgent = new Agent("Yuki", "yuki@example.com", maxOpenTickets: 8, context.Clock.UtcNow);
        inactiveAgent.Deactivate();
        SetId(inactiveAgent, 6);

        var eligibleAgent = new Agent("Ben", "ben@example.com", maxOpenTickets: 8, context.Clock.UtcNow);
        SetId(eligibleAgent, 2);

        context.Agents.Setup(a => a.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { eligibleAgent }); // Only Ben is active

        var ticket = context.ExistingTicket(
            new TicketBuilder()
                .WithPriority(TicketPriority.Medium)
                .AssignedTo(6)
                .Build());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(2, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsCritical_ThrowsBusinessRuleViolation()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Critical).Build());

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None));

        Assert.Contains("Critical", ex.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsResolved_ThrowsBusinessRuleViolation()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithStatus(TicketStatus.Resolved).Build());

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None));

        Assert.Contains("Resolved", ex.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsClosed_ThrowsBusinessRuleViolation()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithStatus(TicketStatus.Closed).Build());

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None));

        Assert.Contains("Closed", ex.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketDoesNotExist_ThrowsNotFound()
    {
        var context = new TicketCommandTestContext();
        context.Tickets.Setup(t => t.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(999, Request(), CancellationToken.None));
    }

    private static void SetId(object entity, int id) =>
        entity.GetType().BaseType!.GetProperty("Id")!.SetValue(entity, id);
}
