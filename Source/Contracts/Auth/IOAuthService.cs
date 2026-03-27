namespace Contracts.Auth;

public interface IOAuthService
{
	Task<string> GetOAuthUrl(string forwardUrl, CancellationToken cancellationToken = default);

	Task<AuthPinResponse> CallbackAsync(int pinId, Guid clientId, CancellationToken cancellationToken = default);

	Task<AuthUserInfo?> GetLoggedInUser();
}
