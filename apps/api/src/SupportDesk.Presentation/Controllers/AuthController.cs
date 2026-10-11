using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Presentation.Authentication;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(IAuthenticationService authenticationService) : ControllerBase
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    /// <summary>Authenticates an active seeded agent and issues a signed JWT.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await authenticationService.LoginAsync(request, ct);
        if (result is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = InvalidCredentialsMessage
            });
        }

        return Ok(result);
    }

    /// <summary>Returns the identity associated with the validated access token.</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<AuthenticatedAgentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public ActionResult<AuthenticatedAgentDto> Me()
    {
        var idClaim = User.FindFirst("agentId")?.Value;
        var nameClaim = User.FindFirst("name")?.Value;
        var emailClaim = User.FindFirst("email")?.Value;

        if (!int.TryParse(idClaim, out var agentId)
            || string.IsNullOrWhiteSpace(nameClaim)
            || string.IsNullOrWhiteSpace(emailClaim))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "The access token does not identify an agent."
            });
        }

        return Ok(new AuthenticatedAgentDto(agentId, nameClaim, emailClaim));
    }
}
