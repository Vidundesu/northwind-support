using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Policies.Tickets;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads <see cref="Agent"/> aggregates.
/// </summary>
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<AgentWorkload>> GetWorkloadsAsync(CancellationToken ct);
}
