using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Policies.Tickets;

/// <summary>Chooses an active, qualified agent with available capacity and the lightest workload.</summary>
public static class AgentAssignmentPolicy
{
    public static bool IsEligible(AgentWorkload agent, Category category)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(category);

        return IsEligible(agent, category, agent.OpenTicketCount);
    }

    public static AgentAssignmentDecision Select(IEnumerable<AgentWorkload> workloads, Category category)
    {
        ArgumentNullException.ThrowIfNull(workloads);
        ArgumentNullException.ThrowIfNull(category);

        var selected = workloads
            .Where(agent => IsEligible(agent, category, agent.OpenTicketCount))
            .OrderBy(agent => agent.OpenTicketCount)
            .ThenBy(agent => agent.AgentId)
            .FirstOrDefault();

        return selected is null
            ? Unassigned(category)
            : new AgentAssignmentDecision(selected.AgentId, selected.FullName, SelectionReason(category));
    }

    /// <summary>
    /// Retains the current agent when eligible. Workload counts include this open ticket, so one
    /// is removed for the keep check; candidates selected as replacements use their full counts.
    /// </summary>
    public static AgentAssignmentDecision KeepCurrentOrSelect(
        IEnumerable<AgentWorkload> workloads,
        Category category,
        int? currentAgentId)
    {
        ArgumentNullException.ThrowIfNull(workloads);
        ArgumentNullException.ThrowIfNull(category);

        var agents = workloads.ToArray();
        if (currentAgentId is { } id)
        {
            var currentAgent = agents.FirstOrDefault(agent => agent.AgentId == id);
            if (currentAgent is not null && IsEligible(currentAgent, category, Math.Max(0, currentAgent.OpenTicketCount - 1)))
            {
                return new AgentAssignmentDecision(
                    currentAgent.AgentId,
                    currentAgent.FullName,
                    $"Kept current agent {currentAgent.FullName}; they remain eligible.");
            }
        }

        var reassignment = Select(agents, category);
        return currentAgentId is null
            ? reassignment
            : reassignment with { Reason = $"Current agent was not eligible. {reassignment.Reason}" };
    }

    private static bool IsEligible(AgentWorkload agent, Category category, int countedOpenTickets) =>
        agent.AgentId > 0 &&
        agent.IsActive &&
        agent.MaxOpenTickets > 0 &&
        countedOpenTickets >= 0 &&
        countedOpenTickets < agent.MaxOpenTickets &&
        (!category.RequiresSpecialist || agent.SupportsCategory(category.Id));

    private static string SelectionReason(Category category) => category.RequiresSpecialist
        ? $"Fewest open tickets among {category.Name} specialists; ties go to the lowest agent ID."
        : "Fewest open tickets among eligible agents; ties go to the lowest agent ID.";

    private static AgentAssignmentDecision Unassigned(Category category) => category.RequiresSpecialist
        ? new AgentAssignmentDecision(null, null,
            $"No available agent has capacity and the required {category.Name} specialization.")
        : new AgentAssignmentDecision(null, null, "No active agent has available ticket capacity.");
}
