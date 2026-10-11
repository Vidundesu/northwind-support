using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Policies.Tickets;

namespace SupportDesk.UnitTests.Domain.Policies.Tickets;

public sealed class AgentAssignmentPolicyTests
{
    private static readonly Category General = new("General", requiresSpecialist: false, forcesCriticalPriority: false);
    private static readonly Category Billing = new("Billing", requiresSpecialist: true, forcesCriticalPriority: false);

    [Fact]
    public void Select_ChoosesTheEligibleAgentWithTheLowestWorkload()
    {
        var decision = AgentAssignmentPolicy.Select(
        [
            Agent(1, openTickets: 4),
            Agent(2, openTickets: 2),
            Agent(3, openTickets: 3)
        ], General);

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void Select_RequiresSpecializationOnlyWhenCategoryRequiresIt()
    {
        var workloads = new[]
        {
            Agent(1, openTickets: 0),
            Agent(2, openTickets: 3, categories: [Billing.Id])
        };

        var specialized = AgentAssignmentPolicy.Select(workloads, Billing);
        var general = AgentAssignmentPolicy.Select(workloads, General);

        Assert.Equal(2, specialized.AgentId);
        Assert.Equal(1, general.AgentId);
    }

    [Fact]
    public void Select_ExcludesAgentsAtCapacity()
    {
        var decision = AgentAssignmentPolicy.Select(
        [
            Agent(1, openTickets: 2, maxOpenTickets: 2),
            Agent(2, openTickets: 1)
        ], General);

        Assert.Equal(2, decision.AgentId);
        Assert.False(AgentAssignmentPolicy.IsEligible(Agent(1, 2, maxOpenTickets: 2), General));
    }

    [Fact]
    public void Select_ExcludesInactiveAgents()
    {
        var decision = AgentAssignmentPolicy.Select(
        [Agent(1, openTickets: 0, isActive: false), Agent(2, openTickets: 1)], General);

        Assert.Equal(2, decision.AgentId);
        Assert.False(AgentAssignmentPolicy.IsEligible(Agent(1, 0, isActive: false), General));
    }

    [Fact]
    public void Select_BreaksWorkloadTiesByLowestAgentId()
    {
        var decision = AgentAssignmentPolicy.Select(
        [Agent(7, openTickets: 2), Agent(3, openTickets: 2)], General);

        Assert.Equal(3, decision.AgentId);
        Assert.Contains("lowest agent ID", decision.Reason);
    }

    [Fact]
    public void Select_WhenNobodyIsEligible_ReturnsAnUnassignedReason()
    {
        var decision = AgentAssignmentPolicy.Select(
        [Agent(1, openTickets: 4, isActive: false)], Billing);

        Assert.Null(decision.AgentId);
        Assert.Null(decision.AgentName);
        Assert.Contains("Billing", decision.Reason);
    }

    [Fact]
    public void KeepCurrentOrSelect_RetainsEligibleCurrentAgentEvenIfAnotherAgentHasLessWork()
    {
        var decision = AgentAssignmentPolicy.KeepCurrentOrSelect(
        [
            Agent(1, openTickets: 2, maxOpenTickets: 2), // Includes the ticket being escalated.
            Agent(2, openTickets: 0)
        ], General, currentAgentId: 1);

        Assert.Equal(1, decision.AgentId);
        Assert.Contains("Kept current agent", decision.Reason);
    }

    [Fact]
    public void KeepCurrentOrSelect_ReassignsWhenCurrentAgentIsNoLongerEligible()
    {
        var decision = AgentAssignmentPolicy.KeepCurrentOrSelect(
        [
            Agent(1, openTickets: 1, isActive: false),
            Agent(2, openTickets: 2)
        ], General, currentAgentId: 1);

        Assert.Equal(2, decision.AgentId);
        Assert.Contains("Current agent was not eligible", decision.Reason);
    }

    [Fact]
    public void KeepCurrentOrSelect_WhenNoReplacementExists_ReturnsUnassignedNormally()
    {
        var decision = AgentAssignmentPolicy.KeepCurrentOrSelect(
            [Agent(1, openTickets: 0, isActive: false)], General, currentAgentId: 1);

        Assert.Null(decision.AgentId);
        Assert.Contains("not eligible", decision.Reason);
        Assert.Contains("No active agent", decision.Reason);
    }

    private static AgentWorkload Agent(
        int id,
        int openTickets,
        int maxOpenTickets = 10,
        bool isActive = true,
        IReadOnlyCollection<int>? categories = null) => new(
            AgentId: id,
            FullName: $"Agent {id}",
            IsActive: isActive,
            MaxOpenTickets: maxOpenTickets,
            OpenTicketCount: openTickets,
            SupportedCategoryIds: categories ?? Array.Empty<int>());
}
