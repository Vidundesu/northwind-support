using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure;

public sealed class SupportDbSeederAuthenticationTests
{
    [Fact]
    public async Task Development_seed_stores_unique_salted_password_hashes_for_all_agents()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await CreateSeeder(db).SeedAsync();

        var agents = await db.Set<Agent>().AsNoTracking().ToListAsync();
        var hasher = new PasswordHasher<Agent>();

        Assert.Equal(6, agents.Count);
        Assert.Equal(agents.Count, agents.Select(agent => agent.PasswordHash).Distinct(StringComparer.Ordinal).Count());

        foreach (var agent in agents)
        {
            Assert.False(string.IsNullOrWhiteSpace(agent.PasswordHash));
            Assert.NotEqual(SupportDbSeeder.DevelopmentPassword, agent.PasswordHash);
            Assert.Equal(
                PasswordVerificationResult.Success,
                hasher.VerifyHashedPassword(agent, agent.PasswordHash, SupportDbSeeder.DevelopmentPassword));
            Assert.Equal(
                PasswordVerificationResult.Failed,
                hasher.VerifyHashedPassword(agent, agent.PasswordHash, "Wrong-Development-Password"));
        }
    }

    [Fact]
    public async Task Existing_seeded_database_backfills_missing_hashes_before_skipping_ticket_seed()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new SupportDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            await CreateSeeder(db).SeedAsync();
        }

        // Simulate existing agents after the migration adds an empty hash column.
        await using (var db = new SupportDbContext(options))
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE Agents SET PasswordHash = ''");
        }

        await using (var db = new SupportDbContext(options))
        {
            await CreateSeeder(db).SeedAsync();

            Assert.Equal(40, await db.Set<Ticket>().CountAsync());

            var agents = await db.Set<Agent>().AsNoTracking().ToListAsync();
            var hasher = new PasswordHasher<Agent>();

            Assert.Equal(6, agents.Count);
            Assert.All(agents, agent =>
                Assert.Equal(
                    PasswordVerificationResult.Success,
                    hasher.VerifyHashedPassword(agent, agent.PasswordHash, SupportDbSeeder.DevelopmentPassword)));
        }
    }

    private static SupportDbSeeder CreateSeeder(SupportDbContext db) =>
        new(db, new FixedClock(), NullLogger<SupportDbSeeder>.Instance);
}
