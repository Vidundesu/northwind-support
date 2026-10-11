using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>An immutable audit entry describing one accepted escalation.</summary>
public sealed class TicketEscalation : Entity
{
    private TicketEscalation() { }

    private TicketEscalation(
        int ticketId, TicketPriority fromPriority, TicketPriority toPriority,
        int? fromAgentId, int? toAgentId, DateTime? fromDueAtUtc, DateTime toDueAtUtc,
        string reason, string escalatedBy, DateTime escalatedAtUtc)
    {
        TicketId = ticketId;
        FromPriority = fromPriority;
        ToPriority = toPriority;
        FromAgentId = fromAgentId;
        ToAgentId = toAgentId;
        FromDueAtUtc = fromDueAtUtc;
        ToDueAtUtc = toDueAtUtc;
        Reason = reason;
        EscalatedBy = escalatedBy;
        EscalatedAtUtc = escalatedAtUtc;
    }

    public int TicketId { get; private set; }
    public TicketPriority FromPriority { get; private set; }
    public TicketPriority ToPriority { get; private set; }
    public int? FromAgentId { get; private set; }
    public int? ToAgentId { get; private set; }
    public DateTime? FromDueAtUtc { get; private set; }
    public DateTime ToDueAtUtc { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string EscalatedBy { get; private set; } = string.Empty;
    public DateTime EscalatedAtUtc { get; private set; }

    public static TicketEscalation Record(
        int ticketId, TicketPriority fromPriority, TicketPriority toPriority,
        int? fromAgentId, int? toAgentId, DateTime? fromDueAtUtc, DateTime toDueAtUtc,
        string reason, string escalatedBy, DateTime escalatedAtUtc)
    {
        if (ticketId <= 0) throw new ArgumentOutOfRangeException(nameof(ticketId));
        if ((int)toPriority != (int)fromPriority + 1) throw new ArgumentException("An escalation must advance one priority level.");
        if (fromAgentId is <= 0 || toAgentId is <= 0) throw new ArgumentOutOfRangeException(nameof(fromAgentId));
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 500) throw new ArgumentException("Reason must contain 5 to 500 characters.", nameof(reason));
        if (string.IsNullOrWhiteSpace(escalatedBy) || escalatedBy.Trim().Length > 100) throw new ArgumentException("EscalatedBy is required and limited to 100 characters.", nameof(escalatedBy));
        if (toDueAtUtc.Kind != DateTimeKind.Utc || escalatedAtUtc.Kind != DateTimeKind.Utc || fromDueAtUtc is { Kind: not DateTimeKind.Utc }) throw new ArgumentException("Escalation timestamps must be UTC.");
        return new TicketEscalation(ticketId, fromPriority, toPriority, fromAgentId, toAgentId, fromDueAtUtc, toDueAtUtc, reason.Trim(), escalatedBy.Trim(), escalatedAtUtc);
    }
}
