namespace Contracts.Auth;

public interface IOAuthProvider
{
	Task<string> BeginLoginAsync(string callbackUrl, CancellationToken cancellationToken = default);

	Task<Result<AuthGrant>> CompleteLoginAsync(string callbackQuery, CancellationToken cancellationToken = default);

	Task<AuthSession> GetUserAsync(CancellationToken cancellationToken = default);
}
