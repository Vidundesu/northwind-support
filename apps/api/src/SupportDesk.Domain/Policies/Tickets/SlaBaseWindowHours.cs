using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Policies.Tickets;

/// <summary>Base SLA windows in hours, matching the Sla:BaseWindowHours configuration shape.</summary>
public sealed record SlaBaseWindowHours(int Low, int Medium, int High, int Critical)
{
    public int For(TicketPriority priority) => priority switch
    {
        TicketPriority.Low => Low,
        TicketPriority.Medium => Medium,
        TicketPriority.High => High,
        TicketPriority.Critical => Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown ticket priority.")
    };
}
