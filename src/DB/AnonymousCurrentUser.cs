using Contracts.Identity;
using Domain.Identity;

namespace DB;

internal sealed class AnonymousCurrentUser : ICurrentUser
{
	public bool IsAuthenticated => false;
	public UserId Id => throw new InvalidOperationException("No user is authenticated.");
	public string DisplayName => string.Empty;
	public string AvatarUrl => string.Empty;
	public UserRole Role => UserRole.Member;
}
