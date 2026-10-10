namespace SupportDesk.Domain.Policies.Tickets;

/// <summary>Read-only agent facts needed to make an assignment decision.</summary>
public sealed record AgentWorkload(
    int AgentId,
    string FullName,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTicketCount,
    IReadOnlyCollection<int> SupportedCategoryIds)
{
    public bool SupportsCategory(int categoryId) => SupportedCategoryIds.Contains(categoryId);
}
