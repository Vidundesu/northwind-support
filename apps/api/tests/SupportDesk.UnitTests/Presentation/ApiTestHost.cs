using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

    static ApiTestHost()
    {
        // Program.cs reads configuration while it is still registering services, which happens
        // before WebApplicationFactory applies its in-memory sources. Environment variables are
        // visible from the first line of Program, so the test settings live there. Every test
        // class uses the same values, so sharing them across the process is safe.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Default",
            "Server=127.0.0.1;Database=UnusedApiTest;User Id=test;Password=NotARealPassword123;TrustServerCertificate=True");
        Environment.SetEnvironmentVariable("Authentication__Jwt__Issuer", Issuer);
        Environment.SetEnvironmentVariable("Authentication__Jwt__Audience", Audience);
        Environment.SetEnvironmentVariable("Authentication__Jwt__SigningKey", SigningKey);
        Environment.SetEnvironmentVariable("Authentication__Jwt__LifetimeMinutes", "60");
    }

    public static WebApplicationFactory<Program> Create(Action<IServiceCollection>? configureServices = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");

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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(42, fullName, "alex.turner@northwind-support.example"));
        return client;
    }

    public static string CreateToken(int id, string fullName, string email)
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
