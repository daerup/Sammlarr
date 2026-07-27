using Domain.Identity;

namespace Contracts.Identity;

public interface IUserScope
{
	IDisposable AsUser(User user);

	Task<IDisposable> AsUser(UserId userId, CancellationToken cancellationToken = default);
}
