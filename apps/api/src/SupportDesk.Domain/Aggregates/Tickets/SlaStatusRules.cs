using System.Linq.Expressions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>The authoritative, SQL-composable SLA status derivation.</summary>
public static class SlaStatusRules
{
    private readonly record struct SlaValues(DateTime? DueAtUtc, DateTime? ResolvedAtUtc, TicketStatus Status, int? WindowSeconds);

    public static SlaStatus Derive(Ticket ticket, DateTime nowUtc, int atRiskThresholdPercent)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return Derive(ticket.DueAtUtc, ticket.ResolvedAtUtc, ticket.Status,
            ticket.ActiveSlaWindowSeconds ?? ticket.SlaWindowSeconds, nowUtc, atRiskThresholdPercent);
    }

    public static SlaStatus Derive(DateTime? dueAtUtc, DateTime? resolvedAtUtc, TicketStatus status,
        int? slaWindowSeconds, DateTime nowUtc, int atRiskThresholdPercent) =>
        CreateValuesExpression(nowUtc, atRiskThresholdPercent).Compile()
            (new SlaValues(dueAtUtc, resolvedAtUtc, status, slaWindowSeconds));

    /// <summary>
    /// Creates the rule tree once for both materialized ticket projections and database-side
    /// filtering. Callers must pass the supplied clock; this rule never reads wall time.
    /// </summary>
    public static Expression<Func<Ticket, SlaStatus>> CreateExpression(DateTime nowUtc, int atRiskThresholdPercent)
    {
        Validate(nowUtc, atRiskThresholdPercent);
        var values = CreateValuesExpression(nowUtc, atRiskThresholdPercent);
        var ticket = Expression.Parameter(typeof(Ticket), "ticket");
        var body = new TicketValuesVisitor(values.Parameters[0], ticket).Visit(values.Body)!;
        return Expression.Lambda<Func<Ticket, SlaStatus>>(body, ticket);
    }

    public static Expression<Func<Ticket, bool>> CreatePredicate(
        SlaStatus status, DateTime nowUtc, int atRiskThresholdPercent)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown SLA status.");
        var statusExpression = CreateExpression(nowUtc, atRiskThresholdPercent);
        var equals = Expression.Equal(statusExpression.Body, Expression.Constant(status));
        return Expression.Lambda<Func<Ticket, bool>>(equals, statusExpression.Parameters);
    }

    private static Expression<Func<SlaValues, SlaStatus>> CreateValuesExpression(DateTime nowUtc, int threshold)
    {
        Validate(nowUtc, threshold);
        return values => values.DueAtUtc == null
            ? SlaStatus.NotApplicable
            : values.ResolvedAtUtc != null
                ? values.ResolvedAtUtc <= values.DueAtUtc ? SlaStatus.Met : SlaStatus.Breached
                : values.Status == TicketStatus.Resolved || values.Status == TicketStatus.Closed
                    ? SlaStatus.Breached
                    : nowUtc > values.DueAtUtc
                        ? SlaStatus.Breached
                        : (values.WindowSeconds ?? 0) <= 0
                            ? SlaStatus.WithinSla
                            : values.DueAtUtc <= nowUtc.AddSeconds((double)(values.WindowSeconds ?? 0) * threshold / 100)
                                ? SlaStatus.AtRisk
                                : SlaStatus.WithinSla;
    }

    private sealed class TicketValuesVisitor(ParameterExpression values, ParameterExpression ticket) : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression == values)
            {
                return node.Member.Name switch
                {
                    nameof(SlaValues.DueAtUtc) => Expression.Property(ticket, nameof(Ticket.DueAtUtc)),
                    nameof(SlaValues.ResolvedAtUtc) => Expression.Property(ticket, nameof(Ticket.ResolvedAtUtc)),
                    nameof(SlaValues.Status) => Expression.Property(ticket, nameof(Ticket.Status)),
                    nameof(SlaValues.WindowSeconds) => Expression.Coalesce(
                        Expression.Property(ticket, nameof(Ticket.ActiveSlaWindowSeconds)),
                        Expression.Property(ticket, nameof(Ticket.SlaWindowSeconds))),
                    _ => base.VisitMember(node)
                };
            }
            return base.VisitMember(node);
        }

        protected override Expression VisitParameter(ParameterExpression node) => node == values ? ticket : base.VisitParameter(node);
    }

    private static void Validate(DateTime nowUtc, int threshold)
    {
        if (threshold is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "The at-risk threshold must be between 1 and 100 percent.");
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The evaluation time must be UTC.", nameof(nowUtc));
    }
}
