using Contracts.Identity;
using Core.Identity;
using Domain.Identity;

namespace Core.Tests.Identity;

public class SessionValidatorTests
{
	private static readonly UserId TargetId = UserId.New();

	private static SessionValidator CreateTestee(params User[] users)
	{
		var repo = IUserRepository.Mock();
		repo.GetAllAsync(Any()).Returns(users);
		return new SessionValidator(repo.Object);
	}

	[Test]
	public async Task IsCurrentAsync_WhenRoleMatches_ReturnsTrue()
	{
		var testee = CreateTestee(new User(TargetId, "Ada", "", UserRole.Admin, DateTimeOffset.UnixEpoch));

		await Assert.That(await testee.IsCurrentAsync(TargetId, UserRole.Admin)).IsTrue();
	}

	[Test]
	public async Task IsCurrentAsync_WhenRoleChanged_ReturnsFalse()
	{
		var testee = CreateTestee(new User(TargetId, "Ada", "", UserRole.Member, DateTimeOffset.UnixEpoch));

		await Assert.That(await testee.IsCurrentAsync(TargetId, UserRole.Admin)).IsFalse();
	}

	[Test]
	public async Task IsCurrentAsync_WhenUserRemoved_ReturnsFalse()
	{
		var testee = CreateTestee();

		await Assert.That(await testee.IsCurrentAsync(TargetId, UserRole.Admin)).IsFalse();
	}
}
