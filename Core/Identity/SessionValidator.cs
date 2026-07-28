using Contracts.Identity;
using Domain.Identity;

namespace Core.Identity;

internal sealed class SessionValidator(IUserRepository users) : ISessionValidator
{
	public async Task<bool> IsCurrentAsync(UserId id, UserRole role, CancellationToken cancellationToken = default)
	{
		var all = await users.GetAllAsync(cancellationToken);
		var user = all.FirstOrDefault(candidate => candidate.Id == id);
		return user is not null && user.Role == role;
	}
}
