using SupportDesk.Application.Configuration;
using SupportDesk.Application.Services.Sla;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Services.Sla;

public class SlaPolicyTests
{
    private readonly SlaPolicy _policy = new();

    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void CalculateWindow_StandardCustomer_UsesBaseWindow(TicketPriority priority, double expectedHours)
    {
        var window = _policy.CalculateWindow(priority, CustomerTier.Standard);
        Assert.Equal(TimeSpan.FromHours(expectedHours), window);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, 2)]
    [InlineData(TicketPriority.High, 4)]
    [InlineData(TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.Low, 36)]
    public void CalculateWindow_PremiumCustomer_HalvesWindow(TicketPriority priority, double expectedHours)
    {
        var window = _policy.CalculateWindow(priority, CustomerTier.Premium);
        Assert.Equal(TimeSpan.FromHours(expectedHours), window);
    }

    [Fact]
    public void CalculateWindow_PremiumCustomer_EnforcesOneHourFloor()
    {
        var customOptions = new SlaOptions
        {
            BaseWindowHours = new() { ["Critical"] = 1.5 },
            PremiumCustomerMultiplier = 0.5,
            MinimumWindowHours = 1.0
        };

        var policy = new SlaPolicy(customOptions);
        var window = policy.CalculateWindow(TicketPriority.Critical, CustomerTier.Premium);

        // 1.5 * 0.5 = 0.75 hours, which should be clamped to 1.0 hour.
        Assert.Equal(TimeSpan.FromHours(1.0), window);
    }

    [Fact]
    public void CalculateDueAt_AddsWindowToFromTime()
    {
        var fromTime = FixedClock.DefaultNow;
        var dueAt = _policy.CalculateDueAt(fromTime, TicketPriority.High, CustomerTier.Standard);

        Assert.Equal(fromTime.AddHours(8), dueAt);
    }
}
