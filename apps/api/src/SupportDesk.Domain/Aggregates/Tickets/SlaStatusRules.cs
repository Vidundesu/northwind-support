namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>Derives SLA status from a ticket and an explicitly supplied UTC clock value.</summary>
public static class SlaStatusRules
{
    /// <summary>
    /// Evaluates the ticket using the configured percentage of its original SLA window as the
    /// at-risk interval. Thresholds are whole percentages from 1 through 100.
    /// </summary>
    public static SlaStatus Derive(Ticket ticket, DateTime nowUtc, int atRiskThresholdPercent)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return Derive(
            ticket.DueAtUtc,
            ticket.ResolvedAtUtc,
            ticket.Status,
            ticket.SlaWindowSeconds,
            nowUtc,
            atRiskThresholdPercent);
    }

    /// <summary>
    /// Evaluates the persisted SLA fields. Kept as the single calculation path for callers that
    /// project ticket fields rather than materializing the aggregate.
    /// </summary>
    public static SlaStatus Derive(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        TicketStatus status,
        int? slaWindowSeconds,
        DateTime nowUtc,
        int atRiskThresholdPercent)
    {
        if (atRiskThresholdPercent is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(atRiskThresholdPercent),
                atRiskThresholdPercent,
                "The at-risk threshold must be between 1 and 100 percent.");
        }

        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The evaluation time must be UTC.", nameof(nowUtc));
        }

        if (dueAtUtc is not { } due)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is { } resolved)
        {
            return resolved <= due ? SlaStatus.Met : SlaStatus.Breached;
        }

        // A terminal status without its resolution timestamp is inconsistent persisted data.
        // Do not classify it from the current clock as though it were still open.
        if (status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            return SlaStatus.Breached;
        }

        // Equality is still within the SLA. It is at risk when an original duration is known,
        // otherwise it remains WithinSla until time has actually passed the due date.
        if (nowUtc > due)
        {
            return SlaStatus.Breached;
        }

        if (slaWindowSeconds is not > 0)
        {
            return SlaStatus.WithinSla;
        }

        // Tick arithmetic preserves sub-second boundary precision and the maximum int-second
        // window times a percentage fits in Int64 after the percentage division.
        var remainingTicks = due.Ticks - nowUtc.Ticks;
        var atRiskTicks = (long)slaWindowSeconds.Value * TimeSpan.TicksPerSecond
            * atRiskThresholdPercent / 100;

        return remainingTicks <= atRiskTicks ? SlaStatus.AtRisk : SlaStatus.WithinSla;
    }
}
