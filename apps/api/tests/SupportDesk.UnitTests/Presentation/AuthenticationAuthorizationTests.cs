using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Presentation.Authentication;

namespace SupportDesk.UnitTests.Presentation;

public sealed class AuthenticationAuthorizationTests
{
    private const string Issuer = "NorthwindSupportDesk.AuthTests";
    private const string Audience = "NorthwindSupportDesk.Web.AuthTests";
    private const string SigningKey = "integration-test-signing-key-at-least-32-bytes-long";

    [Fact]
    public async Task MeRejectsRequestsWithoutToken()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BusinessEndpointIsProtectedByFallbackPolicy()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MeReturnsAgentForValidToken()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(42, "Alex Turner", "alex.turner@northwind-support.example"));

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<AuthenticatedAgentDto>();
        Assert.NotNull(user);
        Assert.Equal(42, user.Id);
        Assert.Equal("Alex Turner", user.FullName);
        Assert.Equal("alex.turner@northwind-support.example", user.Email);
    }

    [Fact]
    public async Task LoginRemainsPublicAndReturnsGenericUnauthorizedForMissingCredentials()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/auth/login",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password.", body, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] =
                        "Server=127.0.0.1;Database=UnusedAuthTest;User Id=test;Password=NotARealPassword123;TrustServerCertificate=True",
                    ["Authentication:Jwt:Issuer"] = Issuer,
                    ["Authentication:Jwt:Audience"] = Audience,
                    ["Authentication:Jwt:SigningKey"] = SigningKey,
                    ["Authentication:Jwt:LifetimeMinutes"] = "60",
                    ["Sla:BaseWindowHours:Low"] = "72",
                    ["Sla:BaseWindowHours:Medium"] = "24",
                    ["Sla:BaseWindowHours:High"] = "8",
                    ["Sla:BaseWindowHours:Critical"] = "4",
                    ["Sla:PremiumCustomerMultiplier"] = "0.5",
                    ["Sla:MinimumWindowHours"] = "1",
                    ["Sla:AtRiskThresholdPercent"] = "25"
                });
            });
        });

    private static string CreateToken(int id, string fullName, string email)
    {
        var idString = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, idString),
            new Claim("agentId", idString),
            new Claim(JwtRegisteredClaimNames.Name, fullName),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            claims,
            notBefore: DateTime.UtcNow.AddSeconds(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
