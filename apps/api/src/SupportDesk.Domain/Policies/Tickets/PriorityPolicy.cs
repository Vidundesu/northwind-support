using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Policies.Tickets;

/// <summary>Pure rules for initial and escalated ticket priority.</summary>
public static class PriorityPolicy
{
    public static PriorityDecision DetermineInitialPriority(Category category, TicketPriority? requestedPriority)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (requestedPriority is { } requested && !Enum.IsDefined(requested))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedPriority), requested, "Unknown requested priority.");
        }

        if (category.ForcesCriticalPriority)
        {
            return new PriorityDecision(
                TicketPriority.Critical,
                $"Category '{category.Name}' forces Critical priority.");
        }

        if (requestedPriority is { } priority)
        {
            return new PriorityDecision(priority, $"Requested priority {priority} applied.");
        }

        return new PriorityDecision(TicketPriority.Medium, "Default priority Medium applied.");
    }

    public static PriorityDecision NextEscalationPriority(TicketPriority currentPriority) => currentPriority switch
    {
        TicketPriority.Low => new(TicketPriority.Medium, "Escalated from Low to Medium."),
        TicketPriority.Medium => new(TicketPriority.High, "Escalated from Medium to High."),
        TicketPriority.High => new(TicketPriority.Critical, "Escalated from High to Critical."),
        TicketPriority.Critical => throw new BusinessRuleViolationException("Critical tickets cannot be escalated further."),
        _ => throw new ArgumentOutOfRangeException(nameof(currentPriority), currentPriority, "Unknown current priority.")
    };

    public static PriorityDecision NextEscalationPriority(Ticket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        if (ticket.Status == TicketStatus.Resolved)
        {
            throw new BusinessRuleViolationException("Resolved tickets cannot be escalated.");
        }

        if (ticket.Status == TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException("Closed tickets cannot be escalated.");
        }

        return NextEscalationPriority(ticket.Priority);
    }
}
