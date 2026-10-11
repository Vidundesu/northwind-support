namespace SupportDesk.Domain.Policies.Tickets;

/// <summary>A promised SLA duration and the human-readable policy reason for it.</summary>
public sealed record SlaWindow(TimeSpan Duration, string Reason);
