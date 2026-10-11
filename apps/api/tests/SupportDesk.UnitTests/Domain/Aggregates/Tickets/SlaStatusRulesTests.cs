using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Policies.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public sealed class SlaStatusRulesTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;
    private const int DefaultThresholdPercent = 25;

    [Fact]
    public void Derive_WithoutDueDate_IsNotApplicable()
    {
        var ticket = new TicketBuilder().Build();

        Assert.Equal(SlaStatus.NotApplicable, SlaStatusRules.Derive(ticket, Now, DefaultThresholdPercent));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Derive_ResolvedAtOrBeforeDue_IsMet(int secondsFromDue)
    {
        var dueAt = Now.AddHours(1);
        var ticket = TicketWithWindow(dueAt);
        ticket.ChangeStatus(TicketStatus.Resolved, dueAt.AddSeconds(secondsFromDue));

        Assert.Equal(SlaStatus.Met, SlaStatusRules.Derive(ticket, Now.AddDays(1), DefaultThresholdPercent));
    }

    [Fact]
    public void Derive_ResolvedAfterDue_IsBreached()
    {
        var dueAt = Now.AddHours(1);
        var ticket = TicketWithWindow(dueAt);
        ticket.ChangeStatus(TicketStatus.Resolved, dueAt.AddTicks(1));

        Assert.Equal(SlaStatus.Breached, SlaStatusRules.Derive(ticket, Now, DefaultThresholdPercent));
    }

    [Fact]
    public void Derive_ResolvedWithoutDueDate_IsNotApplicable()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.Resolved).ResolvedAt(Now).Build();

        Assert.Equal(SlaStatus.NotApplicable, SlaStatusRules.Derive(ticket, Now, DefaultThresholdPercent));
    }

    [Fact]
    public void Derive_UnresolvedPastDue_IsBreached()
    {
        var ticket = TicketWithWindow(Now.AddHours(1));

        Assert.Equal(SlaStatus.Breached, SlaStatusRules.Derive(ticket, Now.AddHours(1).AddTicks(1), DefaultThresholdPercent));
    }

    [Fact]
    public void Derive_AtDueBoundary_IsAtRiskButNotBreachedWhenWindowIsKnown()
    {
        var ticket = TicketWithWindow(Now.AddHours(1));

        Assert.Equal(SlaStatus.AtRisk, SlaStatusRules.Derive(ticket, Now.AddHours(1), DefaultThresholdPercent));
    }

    [Fact]
    public void Derive_AfterEscalation_UsesActiveWindowAndPreservesOriginalWindow()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.High).Build();
        ticket.SetInitialSlaWindow(8 * 60 * 60, Now);
        var escalationAt = Now.AddHours(1);
        var decision = TriageDecision.Create(
            new PriorityDecision(TicketPriority.Critical, "Escalated."),
            new SlaWindow(TimeSpan.FromHours(4), "Critical base window."),
            new AgentAssignmentDecision(null, null, "No eligible agent."),
            escalationAt);

        ticket.ApplyEscalationDecision(decision, escalationAt);

        Assert.Equal(8 * 60 * 60, ticket.SlaWindowSeconds);
        Assert.Equal(4 * 60 * 60, ticket.ActiveSlaWindowSeconds);
        Assert.Equal(SlaStatus.WithinSla,
            SlaStatusRules.Derive(ticket, escalationAt.AddHours(2.5), 25));
    }

    [Fact]
    public void Derive_MoreThanThresholdRemaining_IsWithinSla()
    {
        var dueAt = Now.AddHours(1);
        var ticket = TicketWithWindow(dueAt);

        Assert.Equal(SlaStatus.WithinSla, SlaStatusRules.Derive(ticket, dueAt.AddMinutes(-16), DefaultThresholdPercent));
    }

    [Fact]
    public void Derive_AtThreshold_IsAtRisk()
    {
        var dueAt = Now.AddHours(1);
        var ticket = TicketWithWindow(dueAt);

        Assert.Equal(SlaStatus.AtRisk, SlaStatusRules.Derive(ticket, dueAt.AddMinutes(-15), DefaultThresholdPercent));
    }

    [Fact]
    public void Derive_BelowThreshold_IsAtRisk()
    {
        var dueAt = Now.AddHours(1);
        var ticket = TicketWithWindow(dueAt);

        Assert.Equal(SlaStatus.AtRisk, SlaStatusRules.Derive(ticket, dueAt.AddMinutes(-14), DefaultThresholdPercent));
    }

    [Fact]
    public void Derive_WithoutKnownWindow_DoesNotInventAnAtRiskThreshold()
    {
        var dueAt = Now.AddHours(1);

        // A known due date without its original window is representable as persisted legacy data.
        var status = SlaStatusRules.Derive(
            dueAt, null, TicketStatus.Open, slaWindowSeconds: null, dueAt, DefaultThresholdPercent);

        // Equality is not a breach; without a known duration it cannot be classified AtRisk either.
        Assert.Equal(SlaStatus.WithinSla, status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Derive_InvalidWindow_DoesNotInventAnAtRiskThreshold(int invalidWindowSeconds)
    {
        var dueAt = Now.AddHours(1);

        var status = SlaStatusRules.Derive(
            dueAt, null, TicketStatus.Open, invalidWindowSeconds, dueAt.AddMinutes(-1), DefaultThresholdPercent);

        Assert.Equal(SlaStatus.WithinSla, status);
    }

    [Fact]
    public void Derive_UsesConfiguredThreshold()
    {
        var dueAt = Now.AddHours(1);
        var ticket = TicketWithWindow(dueAt);
        var evaluationTime = dueAt.AddMinutes(-20); // One third of the window remains.

        Assert.Equal(SlaStatus.WithinSla, SlaStatusRules.Derive(ticket, evaluationTime, 25));
        Assert.Equal(SlaStatus.AtRisk, SlaStatusRules.Derive(ticket, evaluationTime, 50));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Derive_RejectsInvalidThreshold(int thresholdPercent)
    {
        var ticket = TicketWithWindow(Now.AddHours(1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => SlaStatusRules.Derive(ticket, Now, thresholdPercent));
    }

    [Fact]
    public void Derive_RejectsNonUtcEvaluationTime()
    {
        var ticket = TicketWithWindow(Now.AddHours(1));

        Assert.Throws<ArgumentException>(
            () => { SlaStatusRules.Derive(ticket, DateTime.SpecifyKind(Now, DateTimeKind.Unspecified), DefaultThresholdPercent); });
    }

    private static Ticket TicketWithWindow(DateTime dueAtUtc)
    {
        var ticket = new TicketBuilder().Build();
        var windowSeconds = checked((int)(dueAtUtc - ticket.CreatedAtUtc).TotalSeconds);
        ticket.SetInitialSlaWindow(windowSeconds, Now);

        // The due date is based on creation time. The helper's requested time must agree with it.
        Assert.Equal(dueAtUtc, ticket.DueAtUtc);
        return ticket;
    }
}
