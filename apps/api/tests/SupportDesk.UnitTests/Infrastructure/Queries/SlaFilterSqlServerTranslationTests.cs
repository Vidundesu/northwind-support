using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class SlaFilterSqlServerTranslationTests
{
    [Fact]
    public void AtRisk_predicate_translates_to_SQL_using_the_same_domain_rule()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer("Server=unused;Database=unused")
            .Options;
        using var db = new SupportDbContext(options);

        var sql = db.Set<Ticket>()
            .Where(SlaStatusRules.CreatePredicate(SlaStatus.AtRisk, FixedClock.DefaultNow, 25))
            .ToQueryString();

        Assert.Contains("DATEADD", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ActiveSlaWindowSeconds", sql, StringComparison.Ordinal);
        Assert.Contains("SlaWindowSeconds", sql, StringComparison.Ordinal);
        Assert.Contains("DueAtUtc", sql, StringComparison.Ordinal);
    }
}
