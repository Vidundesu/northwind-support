using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure;

public sealed class TicketEscalationPersistenceTests
{
    [Fact]
    public async Task Escalation_history_round_trips_with_optional_agent_references()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).Options;
        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await db.AddAsync(new Customer("Contoso", "customer@example.test", null, CustomerTier.Standard, FixedClock.DefaultNow));
        await db.AddAsync(new Category("General", false, false));
        await db.SaveChangesAsync();
        var ticket = Ticket.Raise("TCK-HISTORY", "Title here", "A long enough description.", 1, 1,
            TicketPriority.Low, FixedClock.DefaultNow);
        ticket.SetInitialSlaWindow(3600, FixedClock.DefaultNow);
        await db.AddAsync(ticket);
        await db.SaveChangesAsync();
        var entry = TicketEscalation.Record(ticket.Id, TicketPriority.Low, TicketPriority.Medium,
            null, null, ticket.DueAtUtc, FixedClock.DefaultNow.AddHours(2), "Customer impact", "team-lead", FixedClock.DefaultNow);

        await db.AddAsync(entry);
        await db.SaveChangesAsync();
        var loaded = await db.Set<TicketEscalation>().AsNoTracking().SingleAsync();

        Assert.Equal(ticket.Id, loaded.TicketId);
        Assert.Equal(TicketPriority.Low, loaded.FromPriority);
        Assert.Equal(TicketPriority.Medium, loaded.ToPriority);
        Assert.Null(loaded.FromAgentId);
        Assert.Null(loaded.ToAgentId);
        Assert.Equal("Customer impact", loaded.Reason);
        Assert.Equal("team-lead", loaded.EscalatedBy);
    }
}
