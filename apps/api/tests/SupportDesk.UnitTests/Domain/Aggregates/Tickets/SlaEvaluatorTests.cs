using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnExactDueDate_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedBeforeDueDate_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now.AddMinutes(-5), Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedOneSecondLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddSeconds(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAtExact25PercentBoundary_IsAtRisk()
    {
        // Window = 4 hours (240 min). 25% threshold = 60 min remaining.
        var created = Now.AddHours(-3);
        var due = Now.AddHours(1);

        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(due, null, Now, created));
    }

    [Fact]
    public void Evaluate_OpenWithLessThan25PercentRemaining_IsAtRisk()
    {
        // Window = 4 hours (240 min). Remaining = 30 min (< 25%).
        var created = Now.AddHours(-3.5);
        var due = Now.AddMinutes(30);

        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(due, null, Now, created));
    }

    [Fact]
    public void Evaluate_OpenWithMoreThan25PercentRemaining_IsWithinSla()
    {
        // Window = 4 hours (240 min). 25% = 60 min. Remaining = 61 min (> 25%).
        var created = Now.AddHours(-3);
        var due = Now.AddMinutes(61);

        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(due, null, Now, created));
    }

    [Fact]
    public void Evaluate_OpenAtDueDateZeroRemaining_IsAtRisk()
    {
        // At the exact due timestamp (not yet past it), remaining is 0 (<= 25%).
        var created = Now.AddHours(-4);
        var due = Now;

        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(due, null, Now, created));
    }

    [Fact]
    public void Evaluate_OpenOneSecondPastDue_IsBreached()
    {
        var created = Now.AddHours(-4);
        var due = Now.AddSeconds(-1);

        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(due, null, Now, created));
    }
}
