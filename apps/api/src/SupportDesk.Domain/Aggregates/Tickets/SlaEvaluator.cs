namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Compatibility entry point for callers projecting ticket fields. The domain rules remain
/// authoritative; this class contains no independent SLA calculation.
/// </summary>
public static class SlaEvaluator
{
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        TicketStatus status,
        int? slaWindowSeconds,
        DateTime nowUtc,
        int atRiskThresholdPercent) => SlaStatusRules.Derive(
            dueAtUtc,
            resolvedAtUtc,
            status,
            slaWindowSeconds,
            nowUtc,
            atRiskThresholdPercent);
}
