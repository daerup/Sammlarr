using Contracts.Identity;
using Domain.Identity;

namespace Core.Identity;

internal sealed class SessionValidator(IUserRepository users) : ISessionValidator
{
	public async Task<bool> IsCurrentAsync(UserId id, UserRole role, CancellationToken cancellationToken = default)
	{
		var session = await users.FindSessionAsync(id, cancellationToken);
		return session.Match(found => found.Role == role, () => false);
	}

	public async Task<bool> IsValidAsync(UserId id, UserRole role, DateTimeOffset issuedAt, CancellationToken cancellationToken = default)
	{
		var session = await users.FindSessionAsync(id, cancellationToken);
		return session.Match(found => found.Role == role && !found.RevokedAt.Match(at => issuedAt < WholeSeconds(at), () => false), () => false);
	}

	private static DateTimeOffset WholeSeconds(DateTimeOffset at) => at.AddTicks(-(at.Ticks % TimeSpan.TicksPerSecond));
}
