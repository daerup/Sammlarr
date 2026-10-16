using Contracts.Identity;
using Core.Identity;
using Domain.Identity;

namespace Core.Tests.Identity;

public class SessionValidatorTests
{
	private static readonly UserId TargetId = UserId.New();

	private readonly Mock<IUserRepository> _users = IUserRepository.Mock();

	private SessionValidator Testee => new(_users.Object);

	[Test]
	public async Task IsCurrentAsync_WhenRoleMatches_ReturnsTrue()
	{
		_users.FindRoleAsync(TargetId, Any()).Returns(Some.Of(UserRole.Admin));

		await Assert.That(await Testee.IsCurrentAsync(TargetId, UserRole.Admin)).IsTrue();
	}

	[Test]
	public async Task IsCurrentAsync_WhenRoleChanged_ReturnsFalse()
	{
		_users.FindRoleAsync(TargetId, Any()).Returns(Some.Of(UserRole.Member));

		await Assert.That(await Testee.IsCurrentAsync(TargetId, UserRole.Admin)).IsFalse();
	}

	[Test]
	public async Task IsCurrentAsync_WhenUserRemoved_ReturnsFalse()
	{
		_users.FindRoleAsync(TargetId, Any()).Returns(new None());

		await Assert.That(await Testee.IsCurrentAsync(TargetId, UserRole.Admin)).IsFalse();
	}
}
