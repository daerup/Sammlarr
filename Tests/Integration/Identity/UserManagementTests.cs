using Contracts.Identity;
using Contracts.Settings;
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
	public async Task GetAllAsync_WhenTheCallerIsAMember_IsRejected()
	{
		await host.SignInAs("member");

		await Assert.That(async () => await Testee.GetAllAsync()).Throws<UnauthorizedAccessException>();
	}

	[Test]
	public async Task GetAllAsync_WhenTheCallerIsAnAdmin_ReturnsEveryUser()
	{
		var member = await host.SeedExistingUser("member-1", UserRole.Member, "Bob");

		var users = await Testee.GetAllAsync();

		await Assert.That(users.Any(user => user.Id == member)).IsTrue();
		await Assert.That(users.Any(user => user.Id == TestCurrentUser.DefaultId)).IsTrue();
	}

	[Test]
	public async Task ChangeRoleAsync_WhenPromotingAMember_PersistsTheRoleAndInvalidatesTheirSession()
	{
		var member = await host.SeedExistingUser("member-1", UserRole.Member, "Bob");

		await Testee.ChangeRoleAsync(member, UserRole.Admin);

		var users = await Testee.GetAllAsync();
		await Assert.That(users.Single(user => user.Id == member).Role).IsEqualTo(UserRole.Admin);
		await Assert.That(host.InvalidatedSessions).Contains(member);
	}

	[Test]
	public async Task DeleteAsync_WhenRemovingAMember_DeletesThemAndInvalidatesTheirSession()
	{
		var member = await host.SeedExistingUser("member-1", UserRole.Member, "Bob");

		await Testee.DeleteAsync(member);

		var users = await Testee.GetAllAsync();
		await Assert.That(users.Any(user => user.Id == member)).IsFalse();
		await Assert.That(host.InvalidatedSessions).Contains(member);
	}

	[Test]
	public async Task ChangeRoleAsync_WhenDemotingTheLastAdmin_IsRejected()
	{
		await Assert.That(async () => await Testee.ChangeRoleAsync(TestCurrentUser.DefaultId, UserRole.Member)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task ChangeRoleAsync_WhenAnotherAdminRemains_DemotesTheAdmin()
	{
		var second = await host.SeedExistingUser("admin-2", UserRole.Admin, "Carol");

		await Testee.ChangeRoleAsync(second, UserRole.Member);

		await Assert.That((await Testee.GetAllAsync()).Single(user => user.Id == second).Role).IsEqualTo(UserRole.Member);
	}

	[Test]
	public async Task DeleteAsync_WhenRemovingTheLastAdmin_IsRejected()
	{
		await Assert.That(async () => await Testee.DeleteAsync(TestCurrentUser.DefaultId)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task ChangeRoleAsync_WhenTheCallerLostAdminInTheDatabase_IsRejectedDespiteTheCookie()
	{
		await host.SetDatabaseRole(TestCurrentUser.DefaultId, UserRole.Member);
		var member = await host.SeedExistingUser("member-1", UserRole.Member, "Bob");

		await Assert.That(async () => await Testee.ChangeRoleAsync(member, UserRole.Admin)).Throws<UnauthorizedAccessException>();
	}

	[Test]
	public async Task SetRegistrationModeAsync_WhenTheCallerIsAnAdmin_ChangesIt()
	{
		await Testee.SetRegistrationModeAsync(RegistrationMode.Closed);

		await Assert.That((await host.Resolve<IProviderConfigService>().GetAsync()).RegistrationMode).IsEqualTo(RegistrationMode.Closed);
	}

	[Test]
	public async Task SetRegistrationModeAsync_WhenTheCallerIsAMember_IsRejected()
	{
		await host.SignInAs("member");

		await Assert.That(async () => await Testee.SetRegistrationModeAsync(RegistrationMode.Closed)).Throws<UnauthorizedAccessException>();
	}
}
