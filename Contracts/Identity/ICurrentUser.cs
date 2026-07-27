using Domain.Identity;

namespace Contracts.Identity;

public interface ICurrentUser
{
	bool IsAuthenticated { get; }
	UserId Id { get; }
	string DisplayName { get; }
	string AvatarUrl { get; }
	UserRole Role { get; }
}
