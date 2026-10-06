namespace Contracts.Auth;

public interface IOAuthProvider
{
	Task<Result<string>> BeginLoginAsync(string callbackUrl, CancellationToken cancellationToken = default);

	Task<Result<ExternalLogin>> CompleteLoginAsync(string callbackQuery, CancellationToken cancellationToken = default);
}
