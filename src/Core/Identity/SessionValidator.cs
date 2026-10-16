using Contracts.Identity;
using Domain.Identity;

namespace Core.Identity;

internal sealed class SessionValidator(IUserRepository users) : ISessionValidator
{
	public async Task<bool> IsCurrentAsync(UserId id, UserRole role, CancellationToken cancellationToken = default)
	{
		var found = await users.FindRoleAsync(id, cancellationToken);
		return found.Match(current => current == role, () => false);
	}
}
