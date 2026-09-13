using Contracts.Identity;
using Domain.Identity;
using Domain.Settings;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Identity;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class UserManagementTests(SammlarrTestHost host) : IAsyncDisposable
{
	private IUserManagementService Testee => host.Resolve<IUserManagementService>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task ListUsersAsync_WhenTheCallerIsAMember_IsRejected()
	{
		await host.SignInAs("member");

		await Assert.That(async () => await Testee.ListAsync()).Throws<UnauthorizedAccessException>();
	}

	[Test]
	public async Task ListUsersAsync_WhenTheCallerIsAnAdmin_ReturnsEveryUser()
	{
		var member = await host.SeedExistingUser("member-1", UserRole.Member, "Bob");

		var users = await Testee.ListAsync();

		await Assert.That(users.Any(user => user.Id == member)).IsTrue();
		await Assert.That(users.Any(user => user.Id == TestCurrentUser.DefaultId)).IsTrue();
	}

	[Test]
	public async Task SetRoleAsync_WhenPromotingAMember_PersistsTheRoleAndInvalidatesTheirSession()
	{
		var member = await host.SeedExistingUser("member-1", UserRole.Member, "Bob");

		await Testee.ChangeRoleAsync(member, UserRole.Admin);

		var users = await Testee.ListAsync();
		await Assert.That(users.Single(user => user.Id == member).Role).IsEqualTo(UserRole.Admin);
		await Assert.That(host.InvalidatedSessions).Contains(member);
	}

	[Test]
	public async Task RemoveUserAsync_WhenRemovingAMember_DeletesThemAndInvalidatesTheirSession()
	{
		var member = await host.SeedExistingUser("member-1", UserRole.Member, "Bob");

		await Testee.RemoveAsync(member);

		var users = await Testee.ListAsync();
		await Assert.That(users.Any(user => user.Id == member)).IsFalse();
		await Assert.That(host.InvalidatedSessions).Contains(member);
	}

	[Test]
	public async Task SetRoleAsync_WhenDemotingTheLastAdmin_IsRejected()
	{
		await Assert.That(async () => await Testee.ChangeRoleAsync(TestCurrentUser.DefaultId, UserRole.Member)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task RemoveUserAsync_WhenRemovingTheLastAdmin_IsRejected()
	{
		await Assert.That(async () => await Testee.RemoveAsync(TestCurrentUser.DefaultId)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task ListUsersAsync_WhenTheCallerLostAdminInTheDatabase_IsRejectedDespiteTheCookie()
	{
		await host.SetDatabaseRole(TestCurrentUser.DefaultId, UserRole.Member);
		var member = await host.SeedExistingUser("member-1", UserRole.Member, "Bob");

		await Assert.That(async () => await Testee.ChangeRoleAsync(member, UserRole.Admin)).Throws<UnauthorizedAccessException>();
	}

	[Test]
	public async Task SetRegistrationModeAsync_WhenTheCallerIsAnAdmin_ChangesIt()
	{
		await Testee.SetRegistrationModeAsync(RegistrationMode.Closed);

		await Assert.That(await Testee.GetRegistrationModeAsync()).IsEqualTo(RegistrationMode.Closed);
	}

	[Test]
	public async Task SetRegistrationModeAsync_WhenTheCallerIsAMember_IsRejected()
	{
		await host.SignInAs("member");

		await Assert.That(async () => await Testee.SetRegistrationModeAsync(RegistrationMode.Closed)).Throws<UnauthorizedAccessException>();
	}
}
