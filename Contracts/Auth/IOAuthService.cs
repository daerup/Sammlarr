namespace Contracts.Auth;

public interface IOAuthService
{
	Task<string> BeginLoginAsync(string callbackUrl, CancellationToken cancellationToken = default);

	Task<Result<None>> CompleteLoginAsync(string callbackQuery, CancellationToken cancellationToken = default);

	Task LogoutAsync(CancellationToken cancellationToken = default);

	Task<AuthSession> GetUserAsync(CancellationToken cancellationToken = default);
}
