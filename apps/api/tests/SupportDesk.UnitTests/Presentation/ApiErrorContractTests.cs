using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Presentation;

/// <summary>
/// Checks the status code and error body a client actually receives, through the real
/// pipeline: login failures, missing tokens, and the escalate endpoint's 400 / 404 / 409.
/// </summary>
public sealed class ApiErrorContractTests
{
    private const string Password = "DemoOnly!ChangeMe2026";
    private const string GenericLoginMessage = "Invalid email or password.";

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_BothReturn401WithTheSameMessage()
    {
        var agent = CreateAgent();
        using var factory = ApiTestHost.Create(services => services.AddScoped(_ => AgentRepositoryReturning(agent)));
        using var client = factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { email = agent.Email, password = "Wrong-password-123!" });
        var unknownEmail = await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@northwind-support.example", password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(GenericLoginMessage, await TitleAsync(wrongPassword));
        Assert.Equal(GenericLoginMessage, await TitleAsync(unknownEmail));
    }

    [Fact]
    public async Task Login_InactiveAgentAndOversizedPassword_Return401NotAServerError()
    {
        var inactive = CreateAgent();
        inactive.Deactivate();
        using var factory = ApiTestHost.Create(services => services.AddScoped(_ => AgentRepositoryReturning(inactive)));
        using var client = factory.CreateClient();

        var inactiveLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = inactive.Email, password = Password });
        var oversized = await client.PostAsJsonAsync("/api/auth/login", new { email = inactive.Email, password = new string('a', 100_000) });

        Assert.Equal(HttpStatusCode.Unauthorized, inactiveLogin.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oversized.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401WithAProblemDocument()
    {
        using var factory = ApiTestHost.Create();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Unauthorized", await TitleAsync(response));
    }

    [Fact]
    public async Task Escalate_ShortReason_Returns400WithAFieldError()
    {
        using var factory = ApiTestHost.Create();
        using var client = ApiTestHost.CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/tickets/41/escalate", new { reason = "bad" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("reason", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Escalate_UnknownTicket_Returns404()
    {
        var tickets = new Mock<ITicketRepository>();
        tickets.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((Ticket?)null);
        using var factory = ApiTestHost.Create(services => services.AddScoped(_ => tickets.Object));
        using var client = ApiTestHost.CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/tickets/999/escalate", new { reason = "Customer impact is severe." });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Not Found", await TitleAsync(response));
    }

    [Fact]
    public async Task Escalate_CriticalTicket_Returns409()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Critical).Build();
        var tickets = new Mock<ITicketRepository>();
        tickets.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        using var factory = ApiTestHost.Create(services => services.AddScoped(_ => tickets.Object));
        using var client = ApiTestHost.CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/tickets/41/escalate", new { reason = "Customer impact is severe." });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Conflict", await TitleAsync(response));
    }

    private static IAgentRepository AgentRepositoryReturning(Agent agent)
    {
        var repository = new Mock<IAgentRepository>();
        repository.Setup(r => r.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        return repository.Object;
    }

    private static Agent CreateAgent()
    {
        var agent = new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, DateTime.UtcNow);
        agent.SetPasswordHash(new PasswordHasher<Agent>().HashPassword(agent, Password));
        return agent;
    }

    private static async Task<string?> TitleAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("title").GetString();
    }
}
