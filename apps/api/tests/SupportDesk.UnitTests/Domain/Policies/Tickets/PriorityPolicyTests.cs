using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Policies.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Policies.Tickets;

public sealed class PriorityPolicyTests
{
    [Fact]
    public void DetermineInitialPriority_ForcedCategoryOverridesRequestedPriority()
    {
        var category = new Category("Security", requiresSpecialist: true, forcesCriticalPriority: true);

        var decision = PriorityPolicy.DetermineInitialPriority(category, TicketPriority.Low);

        Assert.Equal(TicketPriority.Critical, decision.Priority);
        Assert.Contains("Security", decision.Reason);
        Assert.Contains("Critical", decision.Reason);
    }

    [Fact]
    public void DetermineInitialPriority_UsesRequestedPriorityWhenNotForced()
    {
        var category = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);

        var decision = PriorityPolicy.DetermineInitialPriority(category, TicketPriority.High);

        Assert.Equal(TicketPriority.High, decision.Priority);
        Assert.Contains("Requested", decision.Reason);
    }

    [Fact]
    public void DetermineInitialPriority_DefaultsToMediumWhenNoPriorityWasRequested()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);

        var decision = PriorityPolicy.DetermineInitialPriority(category, requestedPriority: null);

        Assert.Equal(TicketPriority.Medium, decision.Priority);
        Assert.Contains("Default", decision.Reason);
    }

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void NextEscalationPriority_AdvancesExactlyOneLevel(
        TicketPriority currentPriority,
        TicketPriority expectedPriority)
    {
        var decision = PriorityPolicy.NextEscalationPriority(currentPriority);

        Assert.Equal(expectedPriority, decision.Priority);
        Assert.Contains("Escalated", decision.Reason);
    }

    [Fact]
    public void NextEscalationPriority_RejectsCriticalTickets()
    {
        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => PriorityPolicy.NextEscalationPriority(TicketPriority.Critical));

        Assert.Contains("Critical", exception.Message);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved, "Resolved")]
    [InlineData(TicketStatus.Closed, "Closed")]
    public void NextEscalationPriority_RejectsTerminalTickets(TicketStatus status, string statusName)
    {
        var ticket = new TicketBuilder().WithStatus(status).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => PriorityPolicy.NextEscalationPriority(ticket));

        Assert.Contains(statusName, exception.Message);
    }

    [Fact]
    public void DetermineInitialPriority_RejectsUnknownRequestedEnumValues()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PriorityPolicy.DetermineInitialPriority(category, (TicketPriority)99));
    }
}
