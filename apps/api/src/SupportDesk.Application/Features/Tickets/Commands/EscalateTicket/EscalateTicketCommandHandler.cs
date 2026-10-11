using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Policies.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets, ICategoryRepository categories, ICustomerRepository customers,
    IAgentRepository agents, IUnitOfWork unitOfWork, IClock clock, SlaSettings settings,
    GetTicketQueryHandler getTicket, ITicketQueries ticketQueries)
{
    public async Task<EscalationResultDto> HandleAsync(
        int id, EscalateTicketRequest request, string escalatedBy, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        var actor = escalatedBy?.Trim();
        var errors = new Dictionary<string, string[]>();
        if (reason is null || reason.Length is < 5 or > 500) errors["reason"] = ["Reason must contain 5 to 500 characters."];
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 100) errors["escalatedBy"] = ["EscalatedBy is required and limited to 100 characters."];
        if (errors.Count > 0) throw new ValidationException(errors);

        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);
        var now = clock.UtcNow;
        PriorityDecision priority;
        try { priority = PriorityPolicy.NextEscalationPriority(ticket); }
        catch (BusinessRuleViolationException exception) { throw new ConflictException(exception.Message); }

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);
        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);
        var window = SlaPolicy.Calculate(priority.Priority, customer.Tier, settings);
        var assignment = AgentAssignmentPolicy.KeepCurrentOrSelect(await agents.GetWorkloadsAsync(ct), category, ticket.AssignedAgentId);
        var decision = TriageDecision.Create(priority, window, assignment, now);

        var history = TicketEscalation.Record(ticket.Id, ticket.Priority, decision.Priority,
            ticket.AssignedAgentId, decision.AssignedAgentId, ticket.DueAtUtc, decision.DueAtUtc,
            reason!, actor!, now);
        ticket.ApplyEscalationDecision(decision, now);
        await tickets.AddEscalationAsync(history, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var updated = await getTicket.HandleAsync(id, ct);
        updated.Triage = new TriageSummaryDto(decision.Priority, decision.PriorityReason,
            (decimal)decision.SlaWindow.Duration.TotalHours, decision.SlaWindow.Reason, decision.AssignmentReason);
        var recorded = (await ticketQueries.GetEscalationsAsync(id, ct)).First(e => e.Id == history.Id);
        return new EscalationResultDto(updated, recorded);
    }
}
