using Domain.Identity;
using Domain.Providers;

namespace Contracts.Auth;

public interface ISignInService
{
	Task<Result<string>> BeginAsync(AuthProvider provider, string callbackUrl, CancellationToken cancellationToken = default);

	Task<Result<User>> CompleteAsync(AuthProvider provider, string callbackQuery, CancellationToken cancellationToken = default);

	Task<Result<User>> CompleteSetupAsync(AuthProvider provider, string callbackQuery, string setupCode, CancellationToken cancellationToken = default);

	Task<Result<User>> SignInWithPasswordAsync(string username, string password, CancellationToken cancellationToken = default);

	Task<Result<User>> LinkAsync(AuthProvider provider, string callbackQuery, UserId signedIn, CancellationToken cancellationToken = default);
}
