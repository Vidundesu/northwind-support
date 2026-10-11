using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using SupportDesk.Presentation.Authentication;

namespace SupportDesk.UnitTests.Presentation;

public sealed class AuthenticationAuthorizationTests
{
    [Fact]
    public async Task MeRejectsRequestsWithoutToken()
    {
        using var factory = ApiTestHost.Create();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BusinessEndpointIsProtectedByFallbackPolicy()
    {
        using var factory = ApiTestHost.Create();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MeReturnsAgentForValidToken()
    {
        using var factory = ApiTestHost.Create();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ApiTestHost.CreateToken(42, "Alex Turner", "alex.turner@northwind-support.example"));

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
        using var factory = ApiTestHost.Create();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/auth/login",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password.", body, StringComparison.Ordinal);
    }
}
