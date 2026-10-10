using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Policies.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketTests
{
    private static readonly DateTime Later = FixedClock.DefaultNow.AddHours(3);

    [Fact]
    public void Raise_StartsNewUnassignedAndWithoutADueDate()
    {
        var ticket = Ticket.Raise(
            "TCK-0041", "  Cannot log in  ", "  Happens for every user.  ", 1, 3,
            TicketPriority.High, FixedClock.DefaultNow);

        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Equal("Cannot log in", ticket.Title);
        Assert.Equal("Happens for every user.", ticket.Description);
        Assert.Null(ticket.AssignedAgentId);
        Assert.Null(ticket.DueAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenResolving_StampsResolutionAndUpdateTimes()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.InProgress).Build();

        ticket.ChangeStatus(TicketStatus.Resolved, Later);

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(Later, ticket.ResolvedAtUtc);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
        Assert.False(ticket.IsOpen);
    }

    [Fact]
    public void ChangeStatus_WhenReopening_ClearsTheResolutionTime()
    {
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .ResolvedAt(FixedClock.DefaultNow)
            .Build();

        ticket.ChangeStatus(TicketStatus.InProgress, Later);

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Null(ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenClosingAnUnresolvedTicket_RecordsAResolutionTime()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.Open).Build();

        ticket.ChangeStatus(TicketStatus.Closed, Later);

        Assert.Equal(Later, ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenClosingAResolvedTicket_KeepsTheOriginalResolutionTime()
    {
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .ResolvedAt(FixedClock.DefaultNow)
            .Build();

        ticket.ChangeStatus(TicketStatus.Closed, Later);

        Assert.Equal(FixedClock.DefaultNow, ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenTicketIsAlreadyClosed_Throws()
    {
        var ticket = new TicketBuilder()
            .WithReference("TCK-0026")
            .WithStatus(TicketStatus.Closed)
            .Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => ticket.ChangeStatus(TicketStatus.Open, Later));

        Assert.Contains("TCK-0026", exception.Message);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }

    [Fact]
    public void AssignTo_WithoutAnAgent_TakesTheTicketBack()
    {
        var ticket = new TicketBuilder().AssignedTo(2).Build();

        ticket.AssignTo(null, Later);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void SetInitialSlaWindow_SetsDurationAndDueDateFromCreation()
    {
        var ticket = new TicketBuilder().Build();

        ticket.SetInitialSlaWindow(3600, Later);

        Assert.Equal(3600, ticket.SlaWindowSeconds);
        Assert.Equal(ticket.CreatedAtUtc.AddSeconds(3600), ticket.DueAtUtc);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetInitialSlaWindow_RejectsNonPositiveWindows(int windowSeconds)
    {
        var ticket = new TicketBuilder().Build();

        Assert.Throws<BusinessRuleViolationException>(() => ticket.SetInitialSlaWindow(windowSeconds, Later));
        Assert.Null(ticket.SlaWindowSeconds);
        Assert.Null(ticket.DueAtUtc);
    }

    [Fact]
    public void SetInitialSlaWindow_CannotReplaceTheOriginalWindowAndUnrelatedChangesRetainIt()
    {
        var ticket = new TicketBuilder().Build();
        ticket.SetInitialSlaWindow(3600, Later);

        ticket.AssignTo(7, Later.AddMinutes(1));

        Assert.Equal(3600, ticket.SlaWindowSeconds);
        Assert.Equal(ticket.CreatedAtUtc.AddSeconds(3600), ticket.DueAtUtc);
        Assert.Throws<BusinessRuleViolationException>(() => ticket.SetInitialSlaWindow(7200, Later));
        Assert.Equal(3600, ticket.SlaWindowSeconds);
    }

    [Fact]
    public void Raise_LeavesUnknownSlaWindowNull()
    {
        var ticket = new TicketBuilder().Build();

        Assert.Null(ticket.SlaWindowSeconds);
    }

    [Fact]
    public void ApplyInitialTriage_AppliesPriorityWindowDueDateAndSelectedAgent()
    {
        var ticket = new TicketBuilder().Build();
        var decision = Decision(TicketPriority.High, TimeSpan.FromHours(8), FixedClock.DefaultNow, 12, "Agent 12");

        ticket.ApplyInitialTriage(decision, FixedClock.DefaultNow);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(8 * 60 * 60, ticket.SlaWindowSeconds);
        Assert.Equal(FixedClock.DefaultNow.AddHours(8), ticket.DueAtUtc);
        Assert.Equal(12, ticket.AssignedAgentId);
    }

    [Fact]
    public void ApplyEscalationDecision_RecalculatesDueDateButRetainsOriginalSlaWindow()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).AssignedTo(3).Build();
        ticket.SetInitialSlaWindow(3600, FixedClock.DefaultNow);
        var decision = Decision(TicketPriority.High, TimeSpan.FromHours(8), Later, 7, "Agent 7");

        ticket.ApplyEscalationDecision(decision, Later);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(7, ticket.AssignedAgentId);
        Assert.Equal(Later.AddHours(8), ticket.DueAtUtc);
        Assert.Equal(3600, ticket.SlaWindowSeconds);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void ApplyEscalationDecision_RejectsSkippingPriorityLevels()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();
        var decision = Decision(TicketPriority.High, TimeSpan.FromHours(8), Later, null, null);

        Assert.Throws<BusinessRuleViolationException>(() => ticket.ApplyEscalationDecision(decision, Later));
        Assert.Equal(TicketPriority.Low, ticket.Priority);
    }

    private static TriageDecision Decision(
        TicketPriority priority,
        TimeSpan window,
        DateTime decisionTimeUtc,
        int? agentId,
        string? agentName) => TriageDecision.Create(
            new PriorityDecision(priority, $"Applied {priority} priority."),
            new SlaWindow(window, $"Configured {priority} SLA window."),
            new AgentAssignmentDecision(agentId, agentName, "Assignment policy result."),
            decisionTimeUtc);
}
