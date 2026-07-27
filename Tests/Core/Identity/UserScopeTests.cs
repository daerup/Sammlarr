using Contracts.Identity;
using Core.Identity;
using Domain.Identity;

namespace Core.Tests.Identity;

public class UserScopeTests
{
	[Test]
	public async Task AsUser_ExposesTheUsersRealRole_ThenRestoresOnDispose()
	{
		var currentUserOverride = new CurrentUserOverride();
		var user = new User(UserId.New(), "Ada", "avatar", UserRole.Owner);
		var testee = new UserScope(currentUserOverride, IUserRepository.Mock().Object);

		ICurrentUser? insideScope;
		using (testee.As(user))
		{
			insideScope = currentUserOverride.Value;
		}

		await Assert.That(insideScope!.Id).IsEqualTo(user.Id);
		await Assert.That(insideScope!.Role).IsEqualTo(UserRole.Owner);
		await Assert.That(currentUserOverride.Value).IsNull();
	}
}
