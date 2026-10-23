using Contracts.Auth;
using Contracts.Identity;
using Contracts.Settings;
using Domain.Identity;
using Domain.Providers;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Auth;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class AccountLinkTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly Secret Password = new("correct horse battery");

	private static readonly Secret NewPassword = new("staple and a hinge");

	private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

	private IAccountLinkService Testee => host.Resolve<IAccountLinkService>();

	private IUserManagementService Users => host.Resolve<IUserManagementService>();

	private ISignInService SignIn => host.Resolve<ISignInService>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Before(Test)]
	public async Task Background()
	{
		host.SetCurrentTime(Now);
		await host.SignsInWithSammlarrAccounts();
	}

	[Test]
	public async Task JoinAsync_WithAnInvite_MakesAMemberWhoCanSignIn()
	{
		var link = await InviteAsync("Mira");

		var joined = await Testee.JoinAsync(link.User, link.Token, Password, Password);

		await Assert
			.That(joined)
			.IsSuccess()
			.And.Multiple(async user =>
			{
				await Assert.That(user.DisplayName).IsEqualTo("Mira");
				await Assert.That(user.Role).IsEqualTo(UserRole.Member);
			});
		await Assert.That(await SignIn.SignInWithPasswordAsync("mira", Password)).IsSuccess();
		await Assert.That((await Users.GetPeopleAsync()).Select(Kind)).DoesNotContain("invited");
	}

	[Test]
	public async Task JoinAsync_ASecondTime_IsRefused()
	{
		var link = await InviteAsync("Mira");
		await Testee.JoinAsync(link.User, link.Token, Password, Password);

		await Assert.That(await Testee.JoinAsync(link.User, link.Token, NewPassword, NewPassword)).IsError();
	}

	[Test]
	public async Task InviteAsync_IssuesALinkThatLastsADay()
	{
		var issued = await Users.InviteAsync("Mira");

		await Assert.That(issued).IsSuccess().And.Multiple(async link => await Assert.That(link.Lifetime).IsEqualTo(TimeSpan.FromDays(1)));
	}

	[Test]
	public async Task JoinAsync_AfterTheInviteWasRevoked_IsRefused()
	{
		var link = await InviteAsync("Mira");
		await Users.RevokeInviteAsync("mira");

		await Assert.That(await Testee.JoinAsync(link.User, link.Token, Password, Password)).IsError();
		await Assert.That((await Users.GetPeopleAsync()).Select(Kind)).DoesNotContain("invited");
	}

	[Test]
	public async Task JoinAsync_WithTheEarlierLinkOnceANewOneWasMade_IsRefused()
	{
		var earlier = await InviteAsync("Mira");
		await Users.RenewInviteAsync("mira");

		await Assert.That(await Testee.JoinAsync(earlier.User, earlier.Token, Password, Password)).IsError();
	}

	[Test]
	public async Task JoinAsync_WithPasswordsThatDoNotMatch_SaysSoAndKeepsTheInvite()
	{
		var link = await InviteAsync("Mira");

		var mismatched = await Testee.JoinAsync(link.User, link.Token, Password, NewPassword);

		await Assert.That(mismatched).IsError().And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("The two passwords do not match."));
		await Assert.That(await Testee.JoinAsync(link.User, link.Token, Password, Password)).IsSuccess();
	}

	[Test]
	public async Task JoinAsync_WithATooShortPassword_SaysSoAndKeepsTheInvite()
	{
		var link = await InviteAsync("Mira");

		var refused = await Testee.JoinAsync(link.User, link.Token, new Secret("too short"), new Secret("too short"));

		await Assert.That(refused).IsError().And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("A password needs at least 12 characters."));
		await Assert.That(await Testee.JoinAsync(link.User, link.Token, Password, Password)).IsSuccess();
	}

	[Test]
	public async Task ResetPasswordAsync_SetsTheNewPasswordOnceAndEndsTheirSessions()
	{
		var guest = await host.SeedLocalUser("guest", Password, UserRole.Member);
		var link = IssuedLinkParts.Of(await Users.ResetLinkAsync(guest));

		var reset = await Testee.ResetPasswordAsync(link.User, link.Token, NewPassword, NewPassword);

		await Assert.That(reset).IsSuccess();
		await Assert.That(await SignIn.SignInWithPasswordAsync("guest", NewPassword)).IsSuccess();
		await Assert.That(await SignIn.SignInWithPasswordAsync("guest", Password)).IsError();
		await Assert.That(host.InvalidatedSessions).Contains(guest);
		await Assert.That(await Testee.ResetPasswordAsync(link.User, link.Token, Password, Password)).IsError();
	}

	[Test]
	public async Task ResetPasswordAsync_WithASecondLinkOnceTheFirstWasUsed_IsRefused()
	{
		var guest = await host.SeedLocalUser("guest", Password, UserRole.Member);
		var first = IssuedLinkParts.Of(await Users.ResetLinkAsync(guest));
		var second = IssuedLinkParts.Of(await Users.ResetLinkAsync(guest));
		await Testee.ResetPasswordAsync(first.User, first.Token, NewPassword, NewPassword);

		await Assert.That(await Testee.ResetPasswordAsync(second.User, second.Token, Password, Password)).IsError();
	}

	[Test]
	public async Task InspectAsync_ForAnInviteOnTheResetPage_FindsNothing()
	{
		var link = await InviteAsync("Mira");

		await Assert.That(await Testee.InspectAsync(AccountLinkKind.Reset, link.User, link.Token)).IsNone();
		await Assert.That(await Testee.InspectAsync(AccountLinkKind.Invite, link.User, link.Token)).IsSome();
	}

	[Test]
	public async Task JoinAsync_OnceTheInstallSignsInWithPlex_IsRefused()
	{
		var link = await InviteAsync("Mira");
		var config = host.Resolve<IProviderConfigService>();
		await config.StoreAsync(await config.GetAsync() with { AuthProvider = AuthProvider.Plex });

		await Assert.That(await Testee.JoinAsync(link.User, link.Token, Password, Password)).IsError();
	}

	[Test]
	public async Task JoinAsync_WithAResetLink_IsRefused()
	{
		var guest = await host.SeedLocalUser("guest", Password, UserRole.Member);
		var link = IssuedLinkParts.Of(await Users.ResetLinkAsync(guest));

		await Assert.That(await Testee.JoinAsync(link.User, link.Token, NewPassword, NewPassword)).IsError();
		await Assert.That(await SignIn.SignInWithPasswordAsync("guest", Password)).IsSuccess();
	}

	[Test]
	public async Task ResetPasswordAsync_WithAnInviteLink_IsRefused()
	{
		var link = await InviteAsync("Mira");

		await Assert.That(await Testee.ResetPasswordAsync(link.User, link.Token, Password, Password)).IsError();
		await Assert.That(await Testee.InspectAsync(AccountLinkKind.Invite, link.User, link.Token)).IsSome();
	}

	[Test]
	public async Task JoinAsync_WithSomeoneElsesId_IsRefused()
	{
		var mira = await InviteAsync("Mira");
		var lena = await InviteAsync("Lena");

		await Assert.That(await Testee.JoinAsync(lena.User, mira.Token, Password, Password)).IsError();
		await Assert.That(await Testee.JoinAsync(lena.User, lena.Token, Password, Password)).IsSuccess();
	}

	[Test]
	[Arguments("!!")]
	[Arguments("a")]
	public async Task JoinAsync_WithAMangledToken_IsRefused(string token)
	{
		var link = await InviteAsync("Mira");

		await Assert.That(await Testee.InspectAsync(AccountLinkKind.Invite, link.User, new Secret(token))).IsNone();
		await Assert.That(await Testee.JoinAsync(link.User, new Secret(token), Password, Password)).IsError();
	}

	[Test]
	public async Task SignInWithPasswordAsync_ForSomeoneOnlyInvited_IsRefused()
	{
		await InviteAsync("Mira");

		var result = await SignIn.SignInWithPasswordAsync("mira", Password);

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("Wrong username or password."));
	}

	private static string Kind(Person person)
	{
		return person switch
		{
			JoinedPerson => "joined",
			InvitedPerson => "invited",
		};
	}

	private async Task<IssuedLinkParts> InviteAsync(string username) => IssuedLinkParts.Of(await Users.InviteAsync(username));
}
