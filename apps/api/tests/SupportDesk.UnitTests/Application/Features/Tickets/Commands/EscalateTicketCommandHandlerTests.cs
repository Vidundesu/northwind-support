using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Policies.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public sealed class EscalateTicketCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_UpdatesTicketAndPersistsMatchingHistoryInOneUnitOfWorkSave()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder().WithStatus(TicketStatus.InProgress).WithPriority(TicketPriority.High).AssignedTo(3).Build();
        typeof(SupportDesk.Domain.Common.Entity).GetProperty(nameof(SupportDesk.Domain.Common.Entity.Id))!.SetValue(ticket, 41);
        ticket.SetInitialSlaWindow(8 * 3600, FixedClock.DefaultNow);
        context.ExistingTicket(ticket);
        context.Agents.Setup(a => a.GetWorkloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentWorkload(3, "Current Agent", true, 1, 1, Array.Empty<int>())]);
        context.TicketQueries.Setup(q => q.GetEscalationsAsync(41, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TicketEscalationDto(0, 41, TicketPriority.High, TicketPriority.Critical,
                new AgentSummaryDto(3, "Current Agent"), new AgentSummaryDto(3, "Current Agent"),
                ticket.DueAtUtc, FixedClock.DefaultNow.AddHours(4), "Critical impact", "team-lead", FixedClock.DefaultNow)]);

        var response = await context.EscalateTicket.HandleAsync(41,
            new EscalateTicketRequest("Critical impact", "team-lead"), CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(FixedClock.DefaultNow.AddHours(4), ticket.DueAtUtc);
        Assert.Equal(8 * 3600, ticket.SlaWindowSeconds);
        Assert.Equal(4 * 3600, ticket.ActiveSlaWindowSeconds);
        Assert.Equal(3, ticket.AssignedAgentId);
        Assert.Equal(TicketPriority.High, context.AddedEscalation!.FromPriority);
        Assert.Equal(TicketPriority.Critical, context.AddedEscalation.ToPriority);
        Assert.Equal(ticket.DueAtUtc, response.Escalation.ToDueAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_RejectsInvalidReasonBeforeMutatingTicket()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder().WithPriority(TicketPriority.High).Build();
        typeof(SupportDesk.Domain.Common.Entity).GetProperty(nameof(SupportDesk.Domain.Common.Entity.Id))!.SetValue(ticket, 41);
        context.ExistingTicket(ticket);

        await Assert.ThrowsAsync<SupportDesk.Application.Exceptions.ValidationException>(() =>
            context.EscalateTicket.HandleAsync(41, new EscalateTicketRequest("bad", "lead"), CancellationToken.None));

        Assert.Equal(TicketPriority.High, ticket.Priority);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_CriticalTicketReturnsConflictWithoutMutation()
    {
        var context = new TicketCommandTestContext();
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Critical).Build();
        typeof(SupportDesk.Domain.Common.Entity).GetProperty(nameof(SupportDesk.Domain.Common.Entity.Id))!.SetValue(ticket, 41);
        context.ExistingTicket(ticket);

        await Assert.ThrowsAsync<SupportDesk.Application.Exceptions.ConflictException>(() =>
            context.EscalateTicket.HandleAsync(41, new EscalateTicketRequest("Critical impact", "lead"), CancellationToken.None));

        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        context.VerifySaved(Times.Never());
    }
}
