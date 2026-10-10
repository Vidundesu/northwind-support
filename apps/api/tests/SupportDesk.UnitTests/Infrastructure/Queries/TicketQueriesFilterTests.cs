using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// The ticket list's filters, run as real SQL against an in-memory Sqlite database.
/// </summary>
public sealed class TicketQueriesFilterTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SupportDbContext _db;
    private readonly Customer _contoso;
    private readonly Customer _fabrikam;
    private readonly Category _billing;
    private readonly Category _technical;
    private readonly Agent _alex;

    public TicketQueriesFilterTests()
    {
        _connection.Open();

        _db = new SupportDbContext(
            new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        var now = FixedClock.DefaultNow;
        _contoso = new Customer("Contoso", "help@contoso.test", null, CustomerTier.Premium, now);
        _fabrikam = new Customer("Fabrikam", "help@fabrikam.test", null, CustomerTier.Standard, now);
        _billing = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        _technical = new Category("Technical", requiresSpecialist: false, forcesCriticalPriority: false);
        _alex = new Agent("Alex Turner", "alex@northwind.test", 10, now);

        _db.AddRange(_contoso, _fabrikam, _billing, _technical, _alex);
        _db.SaveChanges();
    }

    [Fact]
    public async Task Priority_is_an_exact_match()
    {
        Add(Ticket("TCK-0001").WithPriority(TicketPriority.Low));
        Add(Ticket("TCK-0002").WithPriority(TicketPriority.High));
        Add(Ticket("TCK-0003").WithPriority(TicketPriority.High));
        Add(Ticket("TCK-0004").WithPriority(TicketPriority.Critical));

        var result = await Query(new TicketQuery { Priority = TicketPriority.High });

        Assert.Equal(new[] { "TCK-0002", "TCK-0003" }, References(result).Order());
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Filters_combine_with_AND()
    {
        Add(Ticket("TCK-0001").ForCustomer(_contoso.Id).WithPriority(TicketPriority.High).WithStatus(TicketStatus.InProgress));
        Add(Ticket("TCK-0002").ForCustomer(_contoso.Id).WithPriority(TicketPriority.High).WithStatus(TicketStatus.Open));
        Add(Ticket("TCK-0003").ForCustomer(_fabrikam.Id).WithPriority(TicketPriority.High).WithStatus(TicketStatus.InProgress));
        Add(Ticket("TCK-0004").ForCustomer(_contoso.Id).WithPriority(TicketPriority.Low).WithStatus(TicketStatus.InProgress));

        var result = await Query(new TicketQuery
        {
            Status = TicketStatus.InProgress,
            Priority = TicketPriority.High,
            CustomerId = _contoso.Id
        });

        Assert.Equal(new[] { "TCK-0001" }, References(result));
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_matches_the_customer_name_and_is_trimmed()
    {
        Add(Ticket("TCK-0001").ForCustomer(_contoso.Id));
        Add(Ticket("TCK-0002").ForCustomer(_fabrikam.Id));

        var result = await Query(new TicketQuery { Search = "  Contoso  " });

        Assert.Equal(new[] { "TCK-0001" }, References(result));
    }

    [Fact]
    public async Task Search_matches_the_reference_and_the_title()
    {
        Add(Ticket("TCK-0042").WithTitle("Printer is jammed"));
        Add(Ticket("TCK-0043").WithTitle("Invoice total is wrong"));

        var byReference = await Query(new TicketQuery { Search = "TCK-0042" });
        var byTitle = await Query(new TicketQuery { Search = "Invoice" });

        Assert.Equal(new[] { "TCK-0042" }, References(byReference));
        Assert.Equal(new[] { "TCK-0043" }, References(byTitle));
    }

    [Fact]
    public async Task A_blank_search_is_ignored()
    {
        Add(Ticket("TCK-0001"));
        Add(Ticket("TCK-0002"));

        var result = await Query(new TicketQuery { Search = "   " });

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Category_and_agent_are_exact_matches_and_unassigned_only_keeps_ownerless_tickets()
    {
        Add(Ticket("TCK-0001").InCategory(_billing.Id).AssignedTo(_alex.Id));
        Add(Ticket("TCK-0002").InCategory(_billing.Id));
        Add(Ticket("TCK-0003").InCategory(_technical.Id).AssignedTo(_alex.Id));

        var byCategory = await Query(new TicketQuery { CategoryId = _billing.Id });
        var byAgent = await Query(new TicketQuery { AssignedAgentId = _alex.Id });
        var unassigned = await Query(new TicketQuery { UnassignedOnly = true });

        Assert.Equal(new[] { "TCK-0001", "TCK-0002" }, References(byCategory).Order());
        Assert.Equal(new[] { "TCK-0001", "TCK-0003" }, References(byAgent).Order());
        Assert.Equal(new[] { "TCK-0002" }, References(unassigned));
    }

    [Fact]
    public async Task The_count_and_the_pages_describe_the_filtered_set()
    {
        for (var i = 1; i <= 12; i++)
        {
            Add(Ticket($"TCK-1{i:000}").WithPriority(TicketPriority.High));
        }

        for (var i = 1; i <= 13; i++)
        {
            Add(Ticket($"TCK-2{i:000}").WithPriority(TicketPriority.Low));
        }

        var lastPage = await Query(new TicketQuery { Priority = TicketPriority.High, Page = 3, PageSize = 5 });

        Assert.Equal(12, lastPage.TotalCount);
        Assert.Equal(3, lastPage.TotalPages);
        Assert.Equal(2, lastPage.Items.Count);
        Assert.All(lastPage.Items, item => Assert.Equal(TicketPriority.High, item.Priority));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static TicketBuilder Ticket(string reference) => new TicketBuilder().WithReference(reference);

    private void Add(TicketBuilder builder)
    {
        _db.Add(builder.Build());
        _db.SaveChanges();
    }

    private Task<PagedResult<TicketListItemDto>> Query(TicketQuery query) =>
        new TicketQueries(_db, new FixedClock()).GetPagedAsync(query.Normalized(), CancellationToken.None);

    private static List<string> References(PagedResult<TicketListItemDto> result) =>
        result.Items.Select(item => item.Reference).ToList();
}
