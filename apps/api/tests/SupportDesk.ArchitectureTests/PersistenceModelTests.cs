using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.ArchitectureTests;

/// <summary>
/// Catches a change to the EF Core mapping that was made without adding a migration, before
/// it reaches a database.
/// </summary>
public class PersistenceModelTests
{
    [Fact]
    public void TheModel_MatchesTheLatestMigration()
    {
        // The connection string is never opened: comparing the model needs no database.
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer("Server=unused;Database=unused")
            .Options;

        using var db = new SupportDbContext(options);

        Assert.False(
            db.Database.HasPendingModelChanges(),
            "The EF model differs from the migrations. Add one with: npm run ef -- migrations add <Name>");
    }

    [Fact]
    public void TicketEscalation_HasHistoryAndLookupConstraints()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer("Server=unused;Database=unused")
            .Options;
        using var db = new SupportDbContext(options);
        var entity = db.Model.FindEntityType(typeof(TicketEscalation));

        Assert.NotNull(entity);
        Assert.Equal("TicketEscalations", entity!.GetTableName());
        Assert.Equal(500, entity.FindProperty(nameof(TicketEscalation.Reason))!.GetMaxLength());
        Assert.Equal(100, entity.FindProperty(nameof(TicketEscalation.EscalatedBy))!.GetMaxLength());
        Assert.Contains(entity.GetIndexes(), index => index.Properties.Select(p => p.Name)
            .SequenceEqual([nameof(TicketEscalation.TicketId), nameof(TicketEscalation.EscalatedAtUtc), "Id"]));
        Assert.Equal(3, entity.GetForeignKeys().Count());
        Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }
}
