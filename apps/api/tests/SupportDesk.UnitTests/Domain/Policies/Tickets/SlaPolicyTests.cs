using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Policies.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Policies.Tickets;

public sealed class SlaPolicyTests
{
    public static TheoryData<TicketPriority, CustomerTier, int> PriorityAndTierWindows => new()
    {
        { TicketPriority.Low, CustomerTier.Standard, 72 },
        { TicketPriority.Medium, CustomerTier.Standard, 24 },
        { TicketPriority.High, CustomerTier.Standard, 8 },
        { TicketPriority.Critical, CustomerTier.Standard, 4 },
        { TicketPriority.Low, CustomerTier.Premium, 36 },
        { TicketPriority.Medium, CustomerTier.Premium, 12 },
        { TicketPriority.High, CustomerTier.Premium, 4 },
        { TicketPriority.Critical, CustomerTier.Premium, 2 }
    };

    private static readonly SlaSettings Settings = new(
        new SlaBaseWindowHours(Low: 72, Medium: 24, High: 8, Critical: 4),
        PremiumCustomerMultiplier: 0.5m,
        MinimumWindowHours: 1,
        AtRiskThresholdPercent: 25);

    [Theory]
    [MemberData(nameof(PriorityAndTierWindows))]
    public void Calculate_UsesConfiguredBaseWindowsAndPremiumMultiplier(
        TicketPriority priority,
        CustomerTier customerTier,
        int expectedHours)
    {
        var window = SlaPolicy.Calculate(priority, customerTier, Settings);

        Assert.Equal(TimeSpan.FromHours(expectedHours), window.Duration);
        Assert.Contains($"{priority} base window", window.Reason);
        if (customerTier == CustomerTier.Premium)
        {
            Assert.Contains("Premium customer", window.Reason);
        }
        else
        {
            Assert.DoesNotContain("Premium", window.Reason);
        }
    }

    [Fact]
    public void Calculate_AppliesMinimumWindowOnlyToPremiumCustomers()
    {
        var oneHourFloorSettings = new SlaSettings(
            new SlaBaseWindowHours(Low: 1, Medium: 2, High: 8, Critical: 4),
            PremiumCustomerMultiplier: 0.5m,
            MinimumWindowHours: 1,
            AtRiskThresholdPercent: 25);

        var oneHourPremium = SlaPolicy.Calculate(TicketPriority.Low, CustomerTier.Premium, oneHourFloorSettings);
        Assert.Equal(TimeSpan.FromHours(1), oneHourPremium.Duration);
        Assert.Contains("1h Premium minimum applied", oneHourPremium.Reason);

        var customFloorSettings = oneHourFloorSettings with { MinimumWindowHours = 2 };
        var premium = SlaPolicy.Calculate(TicketPriority.Low, CustomerTier.Premium, customFloorSettings);
        var standard = SlaPolicy.Calculate(TicketPriority.Low, CustomerTier.Standard, customFloorSettings);

        Assert.Equal(TimeSpan.FromHours(2), premium.Duration);
        Assert.Contains("2h Premium minimum applied", premium.Reason);
        Assert.Equal(TimeSpan.FromHours(1), standard.Duration);
        Assert.DoesNotContain("minimum", standard.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Calculate_UsesConfiguredValuesAndKeepsAtRiskThresholdInTheExistingSettingsContract()
    {
        var settings = new SlaSettings(
            new SlaBaseWindowHours(Low: 48, Medium: 18, High: 6, Critical: 3),
            PremiumCustomerMultiplier: 0.75m,
            MinimumWindowHours: 2,
            AtRiskThresholdPercent: 30);

        var window = SlaPolicy.Calculate(TicketPriority.High, CustomerTier.Premium, settings);

        Assert.Equal(TimeSpan.FromHours(4.5), window.Duration);
        Assert.Equal(30, settings.AtRiskThresholdPercent);
        Assert.Contains("0.75", window.Reason);
    }

    [Fact]
    public void ExistingAtRiskThresholdSettingFeedsThePhaseTwoStatusRule()
    {
        var nowUtc = FixedClock.DefaultNow;
        var ticket = new TicketBuilder().Build();
        ticket.SetInitialSlaWindow(3600, nowUtc);

        var status = SlaStatusRules.Derive(
            ticket,
            ticket.DueAtUtc!.Value.AddMinutes(-15),
            Settings.AtRiskThresholdPercent);

        Assert.Equal(25, Settings.AtRiskThresholdPercent);
        Assert.Equal(SlaStatus.AtRisk, status);
    }

    [Fact]
    public void Calculate_RejectsInvalidSettings()
    {
        var settings = new SlaSettings(
            new SlaBaseWindowHours(Low: 72, Medium: 24, High: 8, Critical: 4),
            PremiumCustomerMultiplier: 0.5m,
            MinimumWindowHours: 1,
            AtRiskThresholdPercent: 0);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => SlaPolicy.Calculate(TicketPriority.Medium, CustomerTier.Standard, settings));
    }
}
