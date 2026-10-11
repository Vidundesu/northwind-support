using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Common;
using SupportDesk.Domain.Repositories;
using SupportDesk.Presentation.Authentication;

namespace SupportDesk.UnitTests.Presentation;

public sealed class AuthenticationServiceTests
{
    private const string Password = "DemoOnly!ChangeMe2026";
    private static readonly JwtSettings Settings = new()
    {
        Issuer = "NorthwindSupportDesk.Tests",
        Audience = "NorthwindSupportDesk.Web.Tests",
        SigningKey = "unit-test-signing-key-must-have-at-least-32-bytes",
        LifetimeMinutes = 60
    };

    [Fact]
    public async Task ActiveAgentWithCorrectPasswordReceivesSignedTokenAndIdentity()
    {
        var agent = CreateAgent();
        var repository = new Mock<IAgentRepository>();
        repository.Setup(r => r.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var service = new AuthenticationService(repository.Object, new PasswordHasher<Agent>(), Settings);

        var response = await service.LoginAsync(
            new LoginRequest("  ALEX.TURNER@NORTHWIND-SUPPORT.EXAMPLE ", Password),
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(agent.Id, response.User.Id);
        Assert.Equal(agent.Email, response.User.Email);
        Assert.Equal(agent.FullName, response.User.FullName);
        Assert.True(response.ExpiresAtUtc > DateTimeOffset.UtcNow);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);
        Assert.Equal(Settings.Issuer, token.Issuer);
        Assert.Equal(Settings.Audience, token.Audiences.Single());
        Assert.Equal(agent.Id.ToString(), token.Claims.Single(claim => claim.Type == "agentId").Value);
        Assert.Equal(agent.FullName, token.Claims.Single(claim => claim.Type == "name").Value);
        Assert.Equal(agent.Email, token.Claims.Single(claim => claim.Type == "email").Value);
    }

    [Fact]
    public async Task UnknownEmailAndWrongPasswordBothFailWithoutIssuingToken()
    {
        var agent = CreateAgent();
        var repository = new Mock<IAgentRepository>();
        repository.Setup(r => r.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var service = new AuthenticationService(repository.Object, new PasswordHasher<Agent>(), Settings);

        var unknownEmail = await service.LoginAsync(
            new LoginRequest("missing@northwind-support.example", Password),
            CancellationToken.None);
        var wrongPassword = await service.LoginAsync(
            new LoginRequest(agent.Email, "Wrong-password-123!"),
            CancellationToken.None);

        Assert.Null(unknownEmail);
        Assert.Null(wrongPassword);
    }

    [Fact]
    public async Task InactiveAgentCannotLoginWithCorrectPassword()
    {
        var agent = CreateAgent();
        agent.Deactivate();

        var repository = new Mock<IAgentRepository>();
        repository.Setup(r => r.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var service = new AuthenticationService(repository.Object, new PasswordHasher<Agent>(), Settings);

        var response = await service.LoginAsync(
            new LoginRequest(agent.Email, Password),
            CancellationToken.None);

        Assert.Null(response);
    }

    private static Agent CreateAgent()
    {
        var agent = new Agent(
            "Alex Turner",
            "alex.turner@northwind-support.example",
            maxOpenTickets: 10,
            DateTime.UtcNow);

        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(agent, 42);
        agent.SetPasswordHash(new PasswordHasher<Agent>().HashPassword(agent, Password));
        return agent;
    }
}
