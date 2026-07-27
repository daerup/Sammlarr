using Domain.Identity;

namespace Contracts.Identity;

public interface IUserScope
{
	IDisposable As(User user);

	Task<IDisposable> As(UserId userId, CancellationToken cancellationToken = default);
}
