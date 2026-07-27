using Domain.Identity;

namespace Contracts.Identity;

public interface IUserScope
{
	IDisposable As(User user);

	IDisposable As(ICurrentUser user);

	Task<IDisposable> AsAsync(UserId userId, CancellationToken cancellationToken = default);
}
