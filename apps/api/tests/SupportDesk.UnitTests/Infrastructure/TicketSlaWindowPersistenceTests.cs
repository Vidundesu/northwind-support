using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure;

public sealed class TicketSlaWindowPersistenceTests
{
    [Fact]
    public async Task Sla_window_round_trips_and_unknown_window_remains_null()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).Options;
        int knownId;
        int unknownId;

        await using (var db = new SupportDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Add(new Customer("Contoso", "sla@contoso.test", null, CustomerTier.Standard, FixedClock.DefaultNow));
            db.Add(new Category("General", requiresSpecialist: false, forcesCriticalPriority: false));
            await db.SaveChangesAsync();
            var known = new TicketBuilder().WithReference("TCK-SLA1").Build();
            known.SetInitialSlaWindow(900, FixedClock.DefaultNow.AddMinutes(1));
            var unknown = new TicketBuilder().WithReference("TCK-SLA2").Build();
            db.AddRange(known, unknown);
            await db.SaveChangesAsync();
            knownId = known.Id;
            unknownId = unknown.Id;
        }

        await using (var db = new SupportDbContext(options))
        {
            Assert.Equal(900, (await db.Set<Ticket>().SingleAsync(t => t.Id == knownId)).SlaWindowSeconds);
            Assert.Equal(900, (await db.Set<Ticket>().SingleAsync(t => t.Id == knownId)).ActiveSlaWindowSeconds);
            Assert.Null((await db.Set<Ticket>().SingleAsync(t => t.Id == unknownId)).SlaWindowSeconds);
        }
    }
}
