namespace SupportDesk.Presentation.Authentication;

public interface IAuthenticationService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
