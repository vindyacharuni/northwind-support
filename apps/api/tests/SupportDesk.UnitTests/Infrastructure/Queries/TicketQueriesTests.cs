using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public class TicketQueriesTests
{
    [Fact]
    public async Task GetPagedAsync_WhenPriorityIsExactMatch_OnlyReturnsMatchingPriority()
    {
        await using var db = await CreateDbAsync();
        var queries = new TicketQueries(db, new FixedClock());

        var customer = new Customer("Acme", "acme@example.com", null, CustomerTier.Standard, FixedClock.DefaultNow);
        var category = new Category("Billing", true, false);
        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        var high = Ticket.Raise("TCK-1001", "Card declined", "Need billing help", customer.Id, category.Id, TicketPriority.High, FixedClock.DefaultNow);
        var low = Ticket.Raise("TCK-1002", "Password reset", "Need help", customer.Id, category.Id, TicketPriority.Low, FixedClock.DefaultNow);
        var high2 = Ticket.Raise("TCK-1003", "Billing error", "Need help again", customer.Id, category.Id, TicketPriority.High, FixedClock.DefaultNow);

        db.AddRange(high, low, high2);
        await db.SaveChangesAsync();

        var result = await queries.GetPagedAsync(new TicketQuery { Priority = TicketPriority.High, Page = 1, PageSize = 20 }, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(TicketPriority.High, item.Priority));
    }

    [Fact]
    public async Task GetPagedAsync_WhenFiltersAreCombined_UsesAndLogic()
    {
        await using var db = await CreateDbAsync();
        var queries = new TicketQueries(db, new FixedClock());

        var customer = new Customer("Northwind", "northwind@example.com", null, CustomerTier.Premium, FixedClock.DefaultNow);
        var otherCustomer = new Customer("Southwind", "southwind@example.com", null, CustomerTier.Standard, FixedClock.DefaultNow);
        var category = new Category("Billing", true, false);
        var otherCategory = new Category("Security", false, true);
        var agent = new Agent("Ada Jones", "ada@example.com", 3, FixedClock.DefaultNow);
        db.AddRange(customer, otherCustomer, category, otherCategory, agent);
        await db.SaveChangesAsync();

        var matching = Ticket.Raise("TCK-2001", "Invoice issue", "Billing problem", customer.Id, category.Id, TicketPriority.High, FixedClock.DefaultNow);
        matching.AssignTo(agent.Id, FixedClock.DefaultNow);
        matching.ChangeStatus(TicketStatus.InProgress, FixedClock.DefaultNow.AddMinutes(1));

        var wrongStatus = Ticket.Raise("TCK-2002", "Invoice issue", "Billing problem", customer.Id, category.Id, TicketPriority.High, FixedClock.DefaultNow);
        wrongStatus.AssignTo(agent.Id, FixedClock.DefaultNow);
        wrongStatus.ChangeStatus(TicketStatus.Resolved, FixedClock.DefaultNow.AddMinutes(1));

        var wrongCustomer = Ticket.Raise("TCK-2003", "Invoice issue", "Billing problem", otherCustomer.Id, category.Id, TicketPriority.High, FixedClock.DefaultNow);
        wrongCustomer.AssignTo(agent.Id, FixedClock.DefaultNow);
        wrongCustomer.ChangeStatus(TicketStatus.InProgress, FixedClock.DefaultNow.AddMinutes(1));

        var unassigned = Ticket.Raise("TCK-2004", "Invoice issue", "Billing problem", customer.Id, category.Id, TicketPriority.High, FixedClock.DefaultNow);
        unassigned.ChangeStatus(TicketStatus.Open, FixedClock.DefaultNow.AddMinutes(1));

        db.AddRange(matching, wrongStatus, wrongCustomer, unassigned);
        await db.SaveChangesAsync();

        var result = await queries.GetPagedAsync(new TicketQuery
        {
            Status = TicketStatus.InProgress,
            Priority = TicketPriority.High,
            CategoryId = category.Id,
            CustomerId = customer.Id,
            AssignedAgentId = agent.Id,
            Page = 1,
            PageSize = 20
        }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(matching.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task GetPagedAsync_WhenSearchingCustomerName_UsesTrimmedCustomerMatch()
    {
        await using var db = await CreateDbAsync();
        var queries = new TicketQueries(db, new FixedClock());

        var customer = new Customer("Alpha Beta", "alpha@example.com", null, CustomerTier.Standard, FixedClock.DefaultNow);
        var otherCustomer = new Customer("Gamma Delta", "gamma@example.com", null, CustomerTier.Standard, FixedClock.DefaultNow);
        var category = new Category("General", false, false);
        db.AddRange(customer, otherCustomer, category);
        await db.SaveChangesAsync();

        var matching = Ticket.Raise("TCK-3001", "Login issue", "Can’t access account", customer.Id, category.Id, TicketPriority.Medium, FixedClock.DefaultNow);
        var other = Ticket.Raise("TCK-3002", "Data issue", "Database error", otherCustomer.Id, category.Id, TicketPriority.Medium, FixedClock.DefaultNow);
        db.AddRange(matching, other);
        await db.SaveChangesAsync();

        var result = await queries.GetPagedAsync(new TicketQuery { Search = "  beta  ", Page = 1, PageSize = 20 }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Alpha Beta", result.Items[0].Customer.Name);
    }
        [Fact]
    public async Task GetPagedAsync_WhenUnassignedOnly_ReturnsOnlyTicketsWithoutAgent()
    {
        await using var db = await CreateDbAsync();
        var queries = new TicketQueries(db, new FixedClock());

        var customer = new Customer("Acme", "acme@example.com", null, CustomerTier.Standard, FixedClock.DefaultNow);
        var category = new Category("General", false, false);
        var agent = new Agent("Ada Jones", "ada@example.com", 3, FixedClock.DefaultNow);
        db.AddRange(customer, category, agent);
        await db.SaveChangesAsync();

        var assigned = Ticket.Raise("TCK-4001", "Assigned one", "Body", customer.Id, category.Id, TicketPriority.Medium, FixedClock.DefaultNow);
        assigned.AssignTo(agent.Id, FixedClock.DefaultNow);
        var unassigned = Ticket.Raise("TCK-4002", "Unassigned one", "Body", customer.Id, category.Id, TicketPriority.Medium, FixedClock.DefaultNow);
        db.AddRange(assigned, unassigned);
        await db.SaveChangesAsync();

        var result = await queries.GetPagedAsync(new TicketQuery { UnassignedOnly = true, Page = 1, PageSize = 20 }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(unassigned.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task GetPagedAsync_WhenSearchingByReference_FindsTheTicket()
    {
        await using var db = await CreateDbAsync();
        var queries = new TicketQueries(db, new FixedClock());

        var customer = new Customer("Acme", "acme@example.com", null, CustomerTier.Standard, FixedClock.DefaultNow);
        var category = new Category("General", false, false);
        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        db.AddRange(
            Ticket.Raise("TCK-0042", "First", "Body", customer.Id, category.Id, TicketPriority.Medium, FixedClock.DefaultNow),
            Ticket.Raise("TCK-0043", "Second", "Body", customer.Id, category.Id, TicketPriority.Medium, FixedClock.DefaultNow));
        await db.SaveChangesAsync();

        var result = await queries.GetPagedAsync(new TicketQuery { Search = "TCK-0042", Page = 1, PageSize = 20 }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("TCK-0042", result.Items[0].Reference);
    }

    [Fact]
    public async Task GetPagedAsync_WhenSearchIsBlank_IgnoresIt()
    {
        await using var db = await CreateDbAsync();
        var queries = new TicketQueries(db, new FixedClock());

        var customer = new Customer("Acme", "acme@example.com", null, CustomerTier.Standard, FixedClock.DefaultNow);
        var category = new Category("General", false, false);
        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        db.AddRange(
            Ticket.Raise("TCK-5001", "One", "Body", customer.Id, category.Id, TicketPriority.Medium, FixedClock.DefaultNow),
            Ticket.Raise("TCK-5002", "Two", "Body", customer.Id, category.Id, TicketPriority.Medium, FixedClock.DefaultNow));
        await db.SaveChangesAsync();

        var result = await queries.GetPagedAsync(new TicketQuery { Search = "   ", Page = 1, PageSize = 20 }, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_CountsAndPagesTheFilteredSet()
    {
        await using var db = await CreateDbAsync();
        var queries = new TicketQueries(db, new FixedClock());

        var customer = new Customer("Acme", "acme@example.com", null, CustomerTier.Standard, FixedClock.DefaultNow);
        var category = new Category("General", false, false);
        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        for (var i = 0; i < 5; i++)
        {
            db.Add(Ticket.Raise($"TCK-60{i:00}", $"High {i}", "Body", customer.Id, category.Id, TicketPriority.High, FixedClock.DefaultNow));
        }

        for (var i = 0; i < 3; i++)
        {
            db.Add(Ticket.Raise($"TCK-70{i:00}", $"Low {i}", "Body", customer.Id, category.Id, TicketPriority.Low, FixedClock.DefaultNow));
        }

        await db.SaveChangesAsync();

        var result = await queries.GetPagedAsync(new TicketQuery { Priority = TicketPriority.High, Page = 1, PageSize = 2 }, CancellationToken.None);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.Items.Count);
    }

    private static async Task<SupportDbContext> CreateDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}
