using Domain.Identity;

namespace Contracts.Auth;

public interface ISignInCookie
{
	Task SignInAsync(UserId user, CancellationToken cancellationToken = default);

	Task SignOutAsync();
}
