using Domain.Identity;
using Domain.Providers;

namespace Contracts.Auth;

public interface ISignInService
{
	Task<string> BeginAsync(AuthProvider provider, string callbackUrl, CancellationToken cancellationToken = default);

	Task<Result<User>> CompleteAsync(AuthProvider provider, string callbackQuery, CancellationToken cancellationToken = default);
}
