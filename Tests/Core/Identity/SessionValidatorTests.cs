using Contracts.Identity;
using Core.Identity;
using Domain.Identity;

namespace Core.Tests.Identity;

public class SessionValidatorTests
{
	private static readonly UserId TargetId = UserId.New();

	private static readonly DateTimeOffset RevokedAt = new(2026, 10, 4, 12, 0, 0, 400, TimeSpan.Zero);

	private readonly Mock<IUserRepository> _users = IUserRepository.Mock();

	private SessionValidator Testee => new(_users.Object);

	[Test]
	public async Task IsCurrentAsync_WhenRoleMatches_ReturnsTrue()
	{
		_users.FindSessionAsync(TargetId, Any()).Returns(Some.Of(new UserSession(UserRole.Admin, new None())));

		await Assert.That(await Testee.IsCurrentAsync(TargetId, UserRole.Admin)).IsTrue();
	}

	[Test]
	public async Task IsCurrentAsync_WhenRoleChanged_ReturnsFalse()
	{
		_users.FindSessionAsync(TargetId, Any()).Returns(Some.Of(new UserSession(UserRole.Member, new None())));

		await Assert.That(await Testee.IsCurrentAsync(TargetId, UserRole.Admin)).IsFalse();
	}

	[Test]
	public async Task IsCurrentAsync_WhenUserRemoved_ReturnsFalse()
	{
		_users.FindSessionAsync(TargetId, Any()).Returns(new None());

		await Assert.That(await Testee.IsCurrentAsync(TargetId, UserRole.Admin)).IsFalse();
	}

	[Test]
	public async Task IsValidAsync_ForACookieIssuedInTheSecondOfTheChange_ReturnsTrue()
	{
		_users.FindSessionAsync(TargetId, Any()).Returns(Some.Of(new UserSession(UserRole.Member, Some.Of(RevokedAt))));

		await Assert.That(await Testee.IsValidAsync(TargetId, UserRole.Member, RevokedAt.AddMilliseconds(-400))).IsTrue();
	}

	[Test]
	public async Task IsValidAsync_WhenNothingWasEverRevoked_ReturnsTrue()
	{
		_users.FindSessionAsync(TargetId, Any()).Returns(Some.Of(new UserSession(UserRole.Member, new None())));

		await Assert.That(await Testee.IsValidAsync(TargetId, UserRole.Member, DateTimeOffset.UnixEpoch)).IsTrue();
	}

	[Test]
	public async Task IsValidAsync_WhenTheRoleChanged_ReturnsFalse()
	{
		_users.FindSessionAsync(TargetId, Any()).Returns(Some.Of(new UserSession(UserRole.Member, new None())));

		await Assert.That(await Testee.IsValidAsync(TargetId, UserRole.Admin, DateTimeOffset.UnixEpoch)).IsFalse();
	}
}
