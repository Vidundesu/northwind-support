using System.Globalization;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Policies.Tickets;

/// <summary>Calculates SLA durations without reading configuration, persistence, or time.</summary>
public static class SlaPolicy
{
    public static SlaWindow Calculate(TicketPriority priority, CustomerTier customerTier, SlaSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown ticket priority.");
        }

        if (!Enum.IsDefined(customerTier))
        {
            throw new ArgumentOutOfRangeException(nameof(customerTier), customerTier, "Unknown customer tier.");
        }

        var baseHours = settings.BaseWindowHours.For(priority);
        var durationHours = (decimal)baseHours;
        var reason = $"{priority} base window {baseHours}h";

        if (customerTier == CustomerTier.Premium)
        {
            var multipliedHours = durationHours * settings.PremiumCustomerMultiplier;
            var minimumHours = (decimal)settings.MinimumWindowHours;
            durationHours = Math.Max(multipliedHours, minimumHours);
            reason += $" multiplied by {settings.PremiumCustomerMultiplier.ToString(CultureInfo.InvariantCulture)} for a Premium customer";

            if (multipliedHours < minimumHours)
            {
                reason += $"; {settings.MinimumWindowHours}h Premium minimum applied";
            }
        }

        if (durationHours > (decimal)TimeSpan.MaxValue.TotalHours)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "The configured SLA window exceeds the supported duration.");
        }

        return new SlaWindow(TimeSpan.FromHours((double)durationHours), $"{reason}.");
    }
}
