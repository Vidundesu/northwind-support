using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Presentation.Authentication;

/// <summary>Authenticates existing agents and issues short-lived signed access tokens.</summary>
public sealed class AuthenticationService(
    IAgentRepository agents,
    IPasswordHasher<Agent> passwordHasher,
    JwtSettings jwtSettings) : IAuthenticationService
{
    // Generous for real credentials; stops a multi-megabyte "password" being hashed in full.
    private const int MaxEmailLength = 254;
    private const int MaxPasswordLength = 128;

    // Hashed once, so an unknown email can cost the same hashing work as a real attempt.
    private static readonly Agent PlaceholderAgent = new("Placeholder", "placeholder@invalid", 1, DateTime.UnixEpoch);
    private static readonly string PlaceholderHash = new PasswordHasher<Agent>().HashPassword(PlaceholderAgent, "placeholder");

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        if (request.Email.Length > MaxEmailLength || request.Password.Length > MaxPasswordLength)
        {
            return null;
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var agent = await agents.GetByEmailAsync(email, cancellationToken);
        if (agent is null)
        {
            // Same work as a wrong password, so response time does not reveal which emails exist.
            passwordHasher.VerifyHashedPassword(PlaceholderAgent, PlaceholderHash, request.Password);
            return null;
        }

        PasswordVerificationResult verification;
        try
        {
            verification = passwordHasher.VerifyHashedPassword(agent, agent.PasswordHash, request.Password);
        }
        catch (FormatException)
        {
            // A corrupt stored hash is treated as invalid credentials, never as a server error.
            return null;
        }

        if (verification == PasswordVerificationResult.Failed || !agent.IsActive)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(jwtSettings.LifetimeMinutes);
        var agentId = agent.Id.ToString(CultureInfo.InvariantCulture);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, agentId),
            new Claim("agentId", agentId),
            new Claim(JwtRegisteredClaimNames.Email, agent.Email),
            new Claim(JwtRegisteredClaimNames.Name, agent.FullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings.Issuer,
            audience: jwtSettings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            new AuthenticatedAgentDto(agent.Id, agent.FullName, agent.Email));
    }
}
