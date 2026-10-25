using Domain.Identity;
using Domain.Providers;

namespace Contracts.Auth;

public interface ISignInService
{
	Task<Result<string>> BeginAsync(AuthProvider provider, string callbackUrl, CancellationToken cancellationToken = default);

	Task<Result<User>> CompleteAsync(AuthProvider provider, string callbackQuery, CancellationToken cancellationToken = default);

	Task<Result<PendingAdmin>> PendAdminAsync(AuthProvider provider, string callbackQuery, CancellationToken cancellationToken = default);

	Optional<PendingAdmin> Pending(Guid id);

	Task<Result<User>> ConfirmAdminAsync(Guid id, Secret setupCode, CancellationToken cancellationToken = default);

	Task<Result<User>> SignInWithPasswordAsync(string username, Secret password, CancellationToken cancellationToken = default);

	Task<Result<User>> LinkAsync(AuthProvider provider, string callbackQuery, UserId signedIn, CancellationToken cancellationToken = default);
}
