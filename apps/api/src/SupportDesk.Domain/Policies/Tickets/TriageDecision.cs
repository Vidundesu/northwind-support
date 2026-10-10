using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Policies.Tickets;

/// <summary>The complete immutable outcome needed to apply ticket triage or recompute escalation.</summary>
public sealed record TriageDecision
{
    private TriageDecision(
        TicketPriority priority,
        string priorityReason,
        SlaWindow slaWindow,
        DateTime dueAtUtc,
        AgentAssignmentDecision assignment)
    {
        Priority = priority;
        PriorityReason = priorityReason;
        SlaWindow = slaWindow;
        DueAtUtc = dueAtUtc;
        Assignment = assignment;
    }

    public TicketPriority Priority { get; }

    public string PriorityReason { get; }

    public SlaWindow SlaWindow { get; }

    public DateTime DueAtUtc { get; }

    public int SlaWindowSeconds => checked((int)(SlaWindow.Duration.Ticks / TimeSpan.TicksPerSecond));

    public AgentAssignmentDecision Assignment { get; }

    public int? AssignedAgentId => Assignment.AgentId;

    public string? AssignedAgentName => Assignment.AgentName;

    public string AssignmentReason => Assignment.Reason;

    public static TriageDecision Create(
        PriorityDecision priority,
        SlaWindow slaWindow,
        AgentAssignmentDecision assignment,
        DateTime decisionTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(priority);
        ArgumentNullException.ThrowIfNull(slaWindow);
        ArgumentNullException.ThrowIfNull(assignment);

        if (decisionTimeUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The decision time must be UTC.", nameof(decisionTimeUtc));
        }

        if (slaWindow.Duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(slaWindow), "The SLA window must be positive.");
        }

        if (slaWindow.Duration.Ticks % TimeSpan.TicksPerSecond != 0 ||
            slaWindow.Duration.Ticks / TimeSpan.TicksPerSecond > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slaWindow),
                "The SLA window must be representable as a positive whole number of seconds for the Ticket aggregate.");
        }

        if (!Enum.IsDefined(priority.Priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), "The triage priority is not recognized.");
        }

        if (assignment.AgentId is <= 0 ||
            (assignment.AgentId is null) != (assignment.AgentName is null) ||
            assignment.AgentId is not null && string.IsNullOrWhiteSpace(assignment.AgentName))
        {
            throw new ArgumentException("The selected agent identity is inconsistent.", nameof(assignment));
        }

        if (string.IsNullOrWhiteSpace(priority.Reason) || string.IsNullOrWhiteSpace(slaWindow.Reason) ||
            string.IsNullOrWhiteSpace(assignment.Reason))
        {
            throw new ArgumentException("A triage decision must explain its priority and assignment.");
        }

        var dueAtUtc = decisionTimeUtc.Add(slaWindow.Duration);
        return new TriageDecision(
            priority.Priority,
            priority.Reason,
            slaWindow,
            dueAtUtc,
            assignment);
    }
}
