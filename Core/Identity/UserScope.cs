using Contracts.Identity;
using Domain.Identity;

namespace Core.Identity;

internal sealed class UserScope(CurrentUserOverride currentUserOverride, IUserRepository users) : IUserScope
{
	public IDisposable As(User user) => As(new SignedInUser(user));

	public IDisposable As(ICurrentUser user)
	{
		var previous = currentUserOverride.Value;
		currentUserOverride.Value = user;
		return new Restore(currentUserOverride, previous);
	}

	public async Task<IDisposable> AsAsync(UserId userId, CancellationToken cancellationToken = default) => As(await users.GetAsync(userId, cancellationToken));

	private sealed class Restore(CurrentUserOverride currentUserOverride, ICurrentUser? previous) : IDisposable
	{
		public void Dispose() => currentUserOverride.Value = previous;
	}
}
