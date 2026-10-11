using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace SupportDesk.UnitTests.Presentation;

/// <summary>
/// Starts the real API pipeline (routing, auth, validation filter, error middleware) in memory.
/// Tests replace repositories with mocks, so no database is needed.
/// </summary>
internal static class ApiTestHost
{
    private const string Issuer = "NorthwindSupportDesk.ApiTests";
    private const string Audience = "NorthwindSupportDesk.Web.ApiTests";
    private const string SigningKey = "api-contract-test-signing-key-at-least-32-bytes";

    public static WebApplicationFactory<Program> Create(Action<IServiceCollection>? configureServices = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] =
                        "Server=127.0.0.1;Database=UnusedApiTest;User Id=test;Password=NotARealPassword123;TrustServerCertificate=True",
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

            if (configureServices is not null)
            {
                // Runs after the application's own registrations, so the last registration wins.
                builder.ConfigureTestServices(configureServices);
            }
        });

    /// <summary>A client carrying a valid token for the named agent.</summary>
    public static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, string fullName = "Alex Turner")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(fullName));
        return client;
    }

    private static string CreateToken(string fullName)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "42"),
            new Claim("agentId", "42"),
            new Claim(JwtRegisteredClaimNames.Name, fullName),
            new Claim(JwtRegisteredClaimNames.Email, "alex.turner@northwind-support.example"),
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
