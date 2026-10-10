namespace SupportDesk.Domain.Policies.Tickets;

/// <summary>
/// Domain view of the existing Sla configuration section. Values retain the configuration's
/// hour and percentage units; adapters can map configuration without coupling this assembly to it.
/// </summary>
public sealed record SlaSettings(
    SlaBaseWindowHours BaseWindowHours,
    decimal PremiumCustomerMultiplier,
    int MinimumWindowHours,
    int AtRiskThresholdPercent)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(BaseWindowHours);

        if (BaseWindowHours.Low <= 0 || BaseWindowHours.Medium <= 0 ||
            BaseWindowHours.High <= 0 || BaseWindowHours.Critical <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BaseWindowHours), "Every base SLA window must be positive.");
        }

        if (PremiumCustomerMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PremiumCustomerMultiplier), "The Premium customer multiplier must be positive.");
        }

        if (MinimumWindowHours <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumWindowHours), "The configured minimum window must be positive.");
        }

        if (AtRiskThresholdPercent is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(AtRiskThresholdPercent), "The at-risk threshold must be between 1 and 100 percent.");
        }
    }
}
