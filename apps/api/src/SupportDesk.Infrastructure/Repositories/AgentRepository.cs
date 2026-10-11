using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Policies.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    /// <summary>Loads the whole aggregate, specializations included.</summary>
    public Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<AgentWorkload>> GetWorkloadsAsync(CancellationToken ct)
    {
        var counts = await db.Set<Ticket>().AsNoTracking()
            .Where(t => t.AssignedAgentId != null && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)
            .GroupBy(t => t.AssignedAgentId!.Value)
            .Select(g => new { AgentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AgentId, x => x.Count, ct);

        var agents = await db.Set<Agent>().AsNoTracking()
            .Include(a => a.Specializations)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);

        return agents.Select(a => new AgentWorkload(a.Id, a.FullName, a.IsActive, a.MaxOpenTickets,
            counts.GetValueOrDefault(a.Id), a.Specializations.Select(s => s.CategoryId).ToArray())).ToArray();
    }
}
