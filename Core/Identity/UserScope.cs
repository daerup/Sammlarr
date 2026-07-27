using Contracts.Identity;
using Domain.Identity;

namespace Core.Identity;

internal sealed class UserScope(CurrentUserOverride currentUserOverride, IUserRepository users) : IUserScope
{
	public IDisposable AsUser(User user)
	{
		var previous = currentUserOverride.Value;
		currentUserOverride.Value = new SignedInUser(user);
		return new Restore(currentUserOverride, previous);
	}

	public async Task<IDisposable> AsUser(UserId userId, CancellationToken cancellationToken = default) =>
		AsUser(await users.GetAsync(userId, cancellationToken));

	private sealed class Restore(CurrentUserOverride currentUserOverride, ICurrentUser? previous) : IDisposable
	{
		public void Dispose() => currentUserOverride.Value = previous;
	}
}
