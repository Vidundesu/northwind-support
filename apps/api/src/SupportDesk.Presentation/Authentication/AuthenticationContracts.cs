namespace SupportDesk.Presentation.Authentication;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record AuthenticatedAgentDto(int Id, string FullName, string Email);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    AuthenticatedAgentDto User);
