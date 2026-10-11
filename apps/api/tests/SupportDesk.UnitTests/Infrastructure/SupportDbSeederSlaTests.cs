using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure;

public sealed class SupportDbSeederSlaTests
{
    [Fact]
    public async Task Development_seed_sets_due_date_original_window_and_active_window_consistently()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).Options;
        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();
        IClock clock = new FixedClock();

        await new SupportDbSeeder(db, clock, NullLogger<SupportDbSeeder>.Instance).SeedAsync();

        var tickets = await db.Set<Ticket>().AsNoTracking().ToListAsync();
        Assert.Equal(40, tickets.Count);
        Assert.All(tickets, ticket =>
        {
            Assert.NotNull(ticket.DueAtUtc);
            Assert.Equal(ticket.SlaWindowSeconds, ticket.ActiveSlaWindowSeconds);
            Assert.Equal(ticket.SlaWindowSeconds, (int)(ticket.DueAtUtc!.Value - ticket.CreatedAtUtc).TotalSeconds);
        });
    }

}
