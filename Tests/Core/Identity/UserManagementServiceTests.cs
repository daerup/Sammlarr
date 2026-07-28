using Contracts.Identity;
using Contracts.Settings;
using Core.Identity;
using Core.Tests.Watchlist;
using Domain.Identity;
using Domain.Settings;
using TestInfrastructure;

namespace Core.Tests.Identity;

public class UserManagementServiceTests
{
	private static UserManagementService CreateTestee(
		Mock<IUserRepository> users,
		ICurrentUser currentUser,
		Mock<IProviderConfigRepository>? config = null,
		Mock<ISessionValidator>? sessionValidator = null,
		Mock<ISessionInvalidator>? sessionInvalidator = null
	) =>
		new(
			users.Object,
			currentUser,
			(config ?? ProviderConfigStubs.PlexWatchlist()).Object,
			(sessionValidator ?? Validator(callerIsAdmin: true)).Object,
			(sessionInvalidator ?? ISessionInvalidator.Mock()).Object
		);

	private static Mock<ISessionValidator> Validator(bool callerIsAdmin)
	{
		var validator = ISessionValidator.Mock();
		validator.IsCurrentAsync(TestCurrentUser.DefaultId, UserRole.Admin, Any()).Returns(callerIsAdmin);
		return validator;
	}

	[Test]
	public async Task ListAsync_WhenNotAdmin_Throws()
	{
		var testee = CreateTestee(IUserRepository.Mock(), new TestCurrentUser { Role = UserRole.Member });

		await Assert.That(async () => await testee.ListAsync()).Throws<UnauthorizedAccessException>();
	}

	[Test]
	public async Task SetRegistrationModeAsync_WhenNotAdmin_Throws()
	{
		var testee = CreateTestee(IUserRepository.Mock(), new TestCurrentUser(), sessionValidator: Validator(callerIsAdmin: false));

		await Assert.That(async () => await testee.SetRegistrationModeAsync(RegistrationMode.Closed)).Throws<UnauthorizedAccessException>();
	}

	[Test]
	public async Task SetRegistrationModeAsync_WhenAdmin_StoresConfig()
	{
		var config = ProviderConfigStubs.PlexWatchlist();
		var testee = CreateTestee(IUserRepository.Mock(), new TestCurrentUser(), config);

		await testee.SetRegistrationModeAsync(RegistrationMode.Closed);

		config.StoreAsync(Any<ProviderConfig>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ChangeRoleAsync_WhenCallerIsNotAdminInDatabase_ThrowsEvenIfCookieClaimsAdmin()
	{
		var targetId = UserId.New();
		var users = IUserRepository.Mock();
		var testee = CreateTestee(users, new TestCurrentUser { Role = UserRole.Admin }, sessionValidator: Validator(callerIsAdmin: false));

		await Assert.That(async () => await testee.ChangeRoleAsync(targetId, UserRole.Admin)).Throws<UnauthorizedAccessException>();
		users.ChangeRoleAsync(targetId, UserRole.Admin, Any()).WasNeverCalled();
	}

	[Test]
	public async Task ChangeRoleAsync_DemotingLastAdmin_Throws()
	{
		var adminId = UserId.New();
		var users = IUserRepository.Mock();
		users.GetAsync(adminId, Any()).Returns(new User(adminId, "Ada", "", UserRole.Admin, DateTimeOffset.UnixEpoch));
		users.CountByRoleAsync(UserRole.Admin, Any()).Returns(1);
		var testee = CreateTestee(users, new TestCurrentUser());

		await Assert.That(async () => await testee.ChangeRoleAsync(adminId, UserRole.Member)).Throws<InvalidOperationException>();
		users.ChangeRoleAsync(adminId, UserRole.Member, Any()).WasNeverCalled();
	}

	[Test]
	public async Task ChangeRoleAsync_PromotingMember_PersistsAndInvalidatesSession()
	{
		var memberId = UserId.New();
		var users = IUserRepository.Mock();
		var invalidator = ISessionInvalidator.Mock();
		var testee = CreateTestee(users, new TestCurrentUser(), sessionInvalidator: invalidator);

		await testee.ChangeRoleAsync(memberId, UserRole.Admin);

		users.ChangeRoleAsync(memberId, UserRole.Admin, Any()).WasCalled(Times.Once);
		invalidator.Invalidate(memberId).WasCalled(Times.Once);
	}

	[Test]
	public async Task RemoveAsync_LastAdmin_Throws()
	{
		var adminId = UserId.New();
		var users = IUserRepository.Mock();
		users.GetAsync(adminId, Any()).Returns(new User(adminId, "Ada", "", UserRole.Admin, DateTimeOffset.UnixEpoch));
		users.CountByRoleAsync(UserRole.Admin, Any()).Returns(1);
		var testee = CreateTestee(users, new TestCurrentUser());

		await Assert.That(async () => await testee.RemoveAsync(adminId)).Throws<InvalidOperationException>();
		users.DeleteAsync(adminId, Any()).WasNeverCalled();
	}

	[Test]
	public async Task RemoveAsync_Member_DeletesAndInvalidatesSession()
	{
		var memberId = UserId.New();
		var users = IUserRepository.Mock();
		users.GetAsync(memberId, Any()).Returns(new User(memberId, "Bob", "", UserRole.Member, DateTimeOffset.UnixEpoch));
		var invalidator = ISessionInvalidator.Mock();
		var testee = CreateTestee(users, new TestCurrentUser(), sessionInvalidator: invalidator);

		await testee.RemoveAsync(memberId);

		users.DeleteAsync(memberId, Any()).WasCalled(Times.Once);
		invalidator.Invalidate(memberId).WasCalled(Times.Once);
	}
}
