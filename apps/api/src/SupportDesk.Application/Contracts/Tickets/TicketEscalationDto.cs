using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

public sealed record TicketEscalationDto(
    int Id, int TicketId, TicketPriority FromPriority, TicketPriority ToPriority,
    AgentSummaryDto? FromAgent, AgentSummaryDto? ToAgent,
    DateTime? FromDueAtUtc, DateTime ToDueAtUtc, string Reason, string EscalatedBy, DateTime EscalatedAtUtc);

public sealed record EscalateTicketRequest(string? Reason)
{
    public const int MinReasonLength = 5;
    public const int MaxReasonLength = 500;
}

public sealed record EscalationResultDto(TicketDetailDto Ticket, TicketEscalationDto Escalation);
