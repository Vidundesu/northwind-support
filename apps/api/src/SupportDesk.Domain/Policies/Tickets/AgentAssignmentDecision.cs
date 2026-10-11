namespace SupportDesk.Domain.Policies.Tickets;

/// <summary>The selected agent, or the normal unassigned outcome, with its explanation.</summary>
public sealed record AgentAssignmentDecision(int? AgentId, string? AgentName, string Reason);
