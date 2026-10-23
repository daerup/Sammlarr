using Contracts.Auth;
using Contracts.Blocklist;
using Contracts.Identity;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Settings;
using Domain.Wantlist;
using Harness;
using Integration.Tests.Auth;
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
		var stamp = await host.SecurityStampOf(member);

		await Testee.ChangeRoleAsync(member, UserRole.Admin);

		var users = await Testee.GetAllAsync();
		await Assert.That(users.Single(user => user.Id == member).Role).IsEqualTo(UserRole.Admin);
		await Assert.That(host.InvalidatedSessions).Contains(member);
		await Assert.That(await host.SecurityStampOf(member)).IsNotEqualTo(stamp);
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
		await Assert.That(await Testee.ChangeRoleAsync(TestCurrentUser.DefaultId, UserRole.Member)).IsError();
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
		await Assert.That(await Testee.DeleteAsync(TestCurrentUser.DefaultId)).IsError();
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

	[Test]
	public async Task GetPeopleAsync_CountsWhatEachPersonHolds()
	{
		await host.WithTmdbAccount("tmdb-key");
		host.SinkAddresses(MediaProvider.Tmdb);
		var wanted = await host.SeedMediaAsync(TmdbId.From("1", MediaType.Movie));
		var blocked = await host.SeedMediaAsync(TmdbId.From("2", MediaType.Movie));
		await host.Resolve<IWantlistService>().AddAsync([wanted], new ManualOrigin(ManualSource.Search));
		await host.Resolve<IBlocklistService>().AddAsync([blocked]);

		var holdings = Joined(await Testee.GetPeopleAsync(), TestCurrentUser.DefaultId).Holdings;

		await Assert.That(holdings.Feeds).IsEqualTo(0);
		await Assert.That(holdings.Wanted).IsEqualTo(1);
		await Assert.That(holdings.Blocklisted).IsEqualTo(1);
		await Assert.That(holdings.Keys).Contains(RemoteService.Tmdb);
	}

	[Test]
	public async Task UnlockAsync_ForALockedAccount_LetsThemSignInAgain()
	{
		host.SetCurrentTime(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
		await host.SignsInWithSammlarrAccounts();
		var guest = await host.SeedLocalUser("guest", new Secret("correct horse battery"), UserRole.Member);
		for (var attempt = 0; attempt < 5; attempt++)
		{
			await host.Resolve<ISignInService>().SignInWithPasswordAsync("guest", new Secret("wrong password!"));
		}

		var locked = Joined(await Testee.GetPeopleAsync(), guest).LockedUntil;
		await Testee.UnlockAsync(guest);

		await Assert.That(locked).IsSome();
		await Assert.That(Joined(await Testee.GetPeopleAsync(), guest).LockedUntil).IsNone();
		await Assert.That(await host.Resolve<ISignInService>().SignInWithPasswordAsync("guest", new Secret("correct horse battery"))).IsSuccess();
	}

	[Test]
	public async Task DeleteWithSinkTitlesAsync_OnAPlexWantlist_RefusesAndKeepsThem()
	{
		var member = await host.SeedExistingUser("member-1", UserRole.Member, "Bob");

		var removed = await Testee.DeleteWithSinkTitlesAsync(member);

		await Assert.That(removed).IsError();
		await Assert.That((await Testee.GetAllAsync()).Any(user => user.Id == member)).IsTrue();
	}

	[Test]
	public async Task GetPeopleAsync_CountsEachPersonsOwnRows()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		var blocked = await host.SeedMediaAsync(TmdbId.From("2", MediaType.Movie));
		await host.Resolve<IBlocklistService>().AddAsync([blocked]);
		await host.SignInAs("member");
		var member = host.CurrentUser.Id;
		await host.Resolve<IBlocklistService>().AddAsync([blocked]);
		await host.Resolve<IBlocklistService>().AddAsync([await host.SeedMediaAsync(TmdbId.From("3", MediaType.Movie))]);
		host.CurrentUser.Id = TestCurrentUser.DefaultId;
		host.CurrentUser.Role = UserRole.Admin;

		var people = await Testee.GetPeopleAsync();

		await Assert.That(Joined(people, TestCurrentUser.DefaultId).Holdings.Blocklisted).IsEqualTo(1);
		await Assert.That(Joined(people, member).Holdings.Blocklisted).IsEqualTo(2);
	}

	[Test]
	public async Task NewAdminCalls_WhenTheCallerIsAMember_AreRejected()
	{
		var target = await host.SeedExistingUser("member-1", UserRole.Member, "Bob");
		await host.SignInAs("member");

		await Assert.That(async () => await Testee.GetPeopleAsync()).Throws<UnauthorizedAccessException>();
		await Assert.That(async () => await Testee.InviteAsync("mira")).Throws<UnauthorizedAccessException>();
		await Assert.That(async () => await Testee.RenewInviteAsync("mira")).Throws<UnauthorizedAccessException>();
		await Assert.That(async () => await Testee.RevokeInviteAsync("mira")).Throws<UnauthorizedAccessException>();
		await Assert.That(async () => await Testee.ResetLinkAsync(target)).Throws<UnauthorizedAccessException>();
		await Assert.That(async () => await Testee.UnlockAsync(target)).Throws<UnauthorizedAccessException>();
		await Assert.That(async () => await Testee.SinkHoldingsAsync(target)).Throws<UnauthorizedAccessException>();
		await Assert.That(async () => await Testee.DeleteWithSinkTitlesAsync(target)).Throws<UnauthorizedAccessException>();
	}

	[Test]
	public async Task InviteAsync_ForAUsernameThatIsTaken_IsRefused()
	{
		await host.SignsInWithSammlarrAccounts();
		await host.SeedLocalUser("guest", new Secret("correct horse battery"), UserRole.Member);

		await Assert.That(await Testee.InviteAsync("Guest")).IsError();
	}

	[Test]
	public async Task InviteAsync_OnAPlexInstall_IsRefused()
	{
		await Assert.That(await Testee.InviteAsync("Mira")).IsError();
	}

	[Test]
	public async Task InviteAsync_ForANameWithAnOpenInvite_IsRefused()
	{
		await host.SignsInWithSammlarrAccounts();
		await Testee.InviteAsync("Mira");

		await Assert.That(await Testee.InviteAsync("Mira")).IsError();
	}

	[Test]
	public async Task InviteAsync_LeavesTheInviteOutOfTheUsers()
	{
		await host.SignsInWithSammlarrAccounts();
		var before = await Testee.CountAsync();

		await Testee.InviteAsync("Mira");

		await Assert.That(await Testee.CountAsync()).IsEqualTo(before);
		await Assert.That((await Testee.GetAllAsync()).Select(user => user.DisplayName)).DoesNotContain("Mira");
	}

	[Test]
	public async Task RevokeInviteAsync_WithTheNameOfSomeoneWhoJoined_RemovesNobody()
	{
		await host.SignsInWithSammlarrAccounts();
		var guest = await host.SeedLocalUser("guest", new Secret("correct horse battery"), UserRole.Member);

		await Testee.RevokeInviteAsync("guest");

		await Assert.That((await Testee.GetAllAsync()).Select(user => user.Id)).Contains(guest);
	}

	[Test]
	public async Task InviteAsync_IssuesALinkWithItsTokenInTheQuery()
	{
		await host.SignsInWithSammlarrAccounts();

		var issued = await Testee.InviteAsync("Mira");

		await Assert.That(issued).IsSuccess().And.Multiple(async link => await Assert.That(link.Path.Reveal()).Matches("^/join/[0-9a-f-]{36}\\?token=[^/&]+$"));
	}

	[Test]
	public async Task ResetLinkAsync_IssuesALinkWithItsTokenInTheQuery()
	{
		await host.SignsInWithSammlarrAccounts();
		var guest = await host.SeedLocalUser("guest", new Secret("correct horse battery"), UserRole.Member);

		var issued = await Testee.ResetLinkAsync(guest);

		await Assert.That(issued).IsSuccess().And.Multiple(async link => await Assert.That(link.Path.Reveal()).Matches("^/reset/[0-9a-f-]{36}\\?token=[^/&]+$"));
	}

	[Test]
	public async Task RenewInviteAsync_MakesALinkThatJoins()
	{
		await host.SignsInWithSammlarrAccounts();
		await Testee.InviteAsync("Mira");

		var renewed = IssuedLinkParts.Of(await Testee.RenewInviteAsync("mira"));

		var joined = await host.Resolve<IAccountLinkService>()
			.JoinAsync(renewed.User, renewed.Token, new Secret("correct horse battery"), new Secret("correct horse battery"));
		await Assert.That(joined).IsSuccess();
	}

	[Test]
	public async Task ResetLinkAsync_ForSomeoneRemovedSince_StopsWorking()
	{
		await host.SignsInWithSammlarrAccounts();
		var guest = await host.SeedLocalUser("guest", new Secret("correct horse battery"), UserRole.Member);
		var link = IssuedLinkParts.Of(await Testee.ResetLinkAsync(guest));
		await Testee.DeleteAsync(guest);

		await Assert
			.That(
				await host.Resolve<IAccountLinkService>()
					.ResetPasswordAsync(link.User, link.Token, new Secret("a brand new password"), new Secret("a brand new password"))
			)
			.IsError();
	}

	private static JoinedPerson Joined(IReadOnlyList<Person> people, UserId id)
	{
		return people
			.Select(person =>
				person switch
				{
					JoinedPerson joined => Some.Of(joined),
					InvitedPerson => (Optional<JoinedPerson>)new None(),
				}
			)
			.WhereSome()
			.Single(joined => joined.User.Id == id);
	}
}
