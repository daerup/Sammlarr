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
	public async Task A_member_cannot_list_the_users()
	{
		await host.SignInAs("member");

		await Assert.That(async () => await Testee.ListAsync()).Throws<UnauthorizedAccessException>();
	}

	[Test]
	public async Task An_admin_lists_every_user()
	{
		var member = await host.SeedExistingUser("member-1", UserRole.Member, "Bob");

		var users = await Testee.ListAsync();

		await Assert.That(users.Any(user => user.Id == member)).IsTrue();
		await Assert.That(users.Any(user => user.Id == TestCurrentUser.DefaultId)).IsTrue();
	}

	[Test]
	public async Task Promoting_a_member_persists_the_new_role_and_invalidates_their_session()
	{
		var member = await host.SeedExistingUser("member-1", UserRole.Member, "Bob");

		await Testee.ChangeRoleAsync(member, UserRole.Admin);

		var users = await Testee.ListAsync();
		await Assert.That(users.Single(user => user.Id == member).Role).IsEqualTo(UserRole.Admin);
		await Assert.That(host.InvalidatedSessions).Contains(member);
	}

	[Test]
	public async Task Removing_a_member_deletes_them_and_invalidates_their_session()
	{
		var member = await host.SeedExistingUser("member-1", UserRole.Member, "Bob");

		await Testee.RemoveAsync(member);

		var users = await Testee.ListAsync();
		await Assert.That(users.Any(user => user.Id == member)).IsFalse();
		await Assert.That(host.InvalidatedSessions).Contains(member);
	}

	[Test]
	public async Task Demoting_the_last_admin_is_rejected()
	{
		await Assert.That(async () => await Testee.ChangeRoleAsync(TestCurrentUser.DefaultId, UserRole.Member)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Removing_the_last_admin_is_rejected()
	{
		await Assert.That(async () => await Testee.RemoveAsync(TestCurrentUser.DefaultId)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task A_caller_who_lost_admin_in_the_database_is_rejected_despite_an_admin_cookie()
	{
		await host.SetDatabaseRole(TestCurrentUser.DefaultId, UserRole.Member);
		var member = await host.SeedExistingUser("member-1", UserRole.Member, "Bob");

		await Assert.That(async () => await Testee.ChangeRoleAsync(member, UserRole.Admin)).Throws<UnauthorizedAccessException>();
	}

	[Test]
	public async Task An_admin_changes_the_registration_mode()
	{
		await Testee.SetRegistrationModeAsync(RegistrationMode.Closed);

		await Assert.That(await Testee.GetRegistrationModeAsync()).IsEqualTo(RegistrationMode.Closed);
	}

	[Test]
	public async Task A_member_cannot_change_the_registration_mode()
	{
		await host.SignInAs("member");

		await Assert.That(async () => await Testee.SetRegistrationModeAsync(RegistrationMode.Closed)).Throws<UnauthorizedAccessException>();
	}
}
