using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Policies.Tickets;

/// <summary>A selected priority together with the reason for applying it.</summary>
public sealed record PriorityDecision(TicketPriority Priority, string Reason);
