using Domain.Identity;

namespace Contracts.Identity;

public static class CurrentUserExtensions
{
	extension(ICurrentUser user)
	{
		public void EnsureAdmin()
		{
			if (user.Role != UserRole.Admin)
			{
				throw new UnauthorizedAccessException("This action requires the Admin role.");
			}
		}
	}
}
