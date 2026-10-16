using Contracts.Identity;
using Core.Identity;
using Domain.Identity;
using TestInfrastructure;

namespace Core.Tests.Identity;

public class UserScopeTests
{
	[Test]
	public async Task AsUser_ExposesTheUsersRealRole_ThenRestoresOnDispose()
	{
		var currentUserOverride = new CurrentUserOverride();
		var user = new User(UserId.New(), "Ada", "avatar", UserRole.Admin, DateTimeOffset.UnixEpoch);
		var testee = new UserScope(currentUserOverride, IUserRepository.Mock().Object);

		Optional<ICurrentUser> insideScope;
		using (testee.As(user))
		{
			insideScope = currentUserOverride.Value;
		}

		var inside = await Assert.That(insideScope).IsSome();
		await Assert.That(inside!.Id).IsEqualTo(user.Id);
		await Assert.That(inside.Role).IsEqualTo(UserRole.Admin);
		await Assert.That(currentUserOverride.Value).IsNone();
	}

	[Test]
	public async Task AsCurrentUser_UsesTheSnapshotDirectly_ThenRestoresOnDispose()
	{
		var currentUserOverride = new CurrentUserOverride();
		var user = new TestCurrentUser();
		var testee = new UserScope(currentUserOverride, IUserRepository.Mock().Object);

		Optional<ICurrentUser> insideScope;
		using (testee.As(user))
		{
			insideScope = currentUserOverride.Value;
		}

		await Assert.That(insideScope).IsSome().And.IsEqualTo(user);
		await Assert.That(currentUserOverride.Value).IsNone();
	}

	[Test]
	public async Task As_InsideAnotherScope_RestoresTheOuterUserThenNobody()
	{
		var currentUserOverride = new CurrentUserOverride();
		var outer = new TestCurrentUser { DisplayName = "Outer" };
		var testee = new UserScope(currentUserOverride, IUserRepository.Mock().Object);

		Optional<ICurrentUser> afterInner;
		using (testee.As(outer))
		{
			using (testee.As(new TestCurrentUser { DisplayName = "Inner" })) { }

			afterInner = currentUserOverride.Value;
		}

		await Assert.That(afterInner).IsSome().And.IsEqualTo(outer);
		await Assert.That(currentUserOverride.Value).IsNone();
	}
}
