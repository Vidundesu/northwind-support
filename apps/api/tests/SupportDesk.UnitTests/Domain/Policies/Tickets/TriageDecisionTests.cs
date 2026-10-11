using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Policies.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Policies.Tickets;

public sealed class TriageDecisionTests
{
    [Fact]
    public void Create_ComposesPolicyResultsAndAnchorsDueDateToSuppliedUtcTime()
    {
        var nowUtc = FixedClock.DefaultNow;
        var priority = new PriorityDecision(TicketPriority.High, "Requested priority High applied.");
        var window = new SlaWindow(TimeSpan.FromHours(8), "High base window 8h.");
        var assignment = new AgentAssignmentDecision(12, "Agent 12", "Fewest open tickets.");

        var decision = TriageDecision.Create(priority, window, assignment, nowUtc);

        Assert.Equal(TicketPriority.High, decision.Priority);
        Assert.Equal(priority.Reason, decision.PriorityReason);
        Assert.Same(window, decision.SlaWindow);
        Assert.Equal(nowUtc.AddHours(8), decision.DueAtUtc);
        Assert.Equal(12, decision.AssignedAgentId);
        Assert.Equal("Agent 12", decision.AssignedAgentName);
        Assert.Equal(assignment.Reason, decision.AssignmentReason);
    }

    [Fact]
    public void Create_PreservesTheNormalUnassignedOutcome()
    {
        var decision = TriageDecision.Create(
            new PriorityDecision(TicketPriority.Medium, "Default priority Medium applied."),
            new SlaWindow(TimeSpan.FromHours(24), "Medium base window 24h."),
            new AgentAssignmentDecision(null, null, "No active agent has available ticket capacity."),
            FixedClock.DefaultNow);

        Assert.Null(decision.AssignedAgentId);
        Assert.Contains("No active agent", decision.AssignmentReason);
    }
}
