using Contracts.Identity;
using Domain.Identity;

namespace Core.Identity;

internal sealed class SignedInUser(User user) : ICurrentUser
{
	public bool IsAuthenticated => true;
	public UserId Id => user.Id;
	public string DisplayName => user.DisplayName;
	public string AvatarUrl => user.AvatarUrl;
	public UserRole Role => user.Role;
}
