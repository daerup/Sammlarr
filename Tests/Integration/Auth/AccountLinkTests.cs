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
	private const string Password = "correct horse battery";

	private const string NewPassword = "staple and a hinge";

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
		var token = await InviteAsync("Mira");

		var joined = await Testee.JoinAsync(token, Password, Password);

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
	public async Task JoinAsync_ForANewMember_ShowsThemInTheirOwnColour()
	{
		var token = await InviteAsync("Mira");
		var before = await host.Resolve<IUserRepository>().GetHuesAsync();

		var mira = (await Testee.JoinAsync(token, Password, Password)).GetOrThrow(error => new InvalidOperationException(error.Reason));

		var person = (await Users.GetPeopleAsync())
			.Select(candidate =>
				candidate switch
				{
					JoinedPerson joined => Some.Of(joined),
					InvitedPerson => (Optional<JoinedPerson>)new None(),
				}
			)
			.WhereSome()
			.Single(joined => joined.User.Id == mira.Id);
		await Assert.That(before.ContainsKey(mira.Id)).IsFalse();
		await Assert.That(person.Hue).IsEqualTo((await host.Resolve<IUserRepository>().GetHuesAsync())[mira.Id]);
		await Assert.That(before.Values).DoesNotContain(person.Hue);
	}

	[Test]
	public async Task JoinAsync_ASecondTime_IsRefused()
	{
		var token = await InviteAsync("Mira");
		await Testee.JoinAsync(token, Password, Password);

		await Assert.That(await Testee.JoinAsync(token, NewPassword, NewPassword)).IsError();
	}

	[Test]
	public async Task JoinAsync_AfterSevenDays_IsRefused()
	{
		var token = await InviteAsync("Mira");
		host.SetCurrentTime(Now.AddDays(7).AddMinutes(1));

		await Assert.That(await Testee.JoinAsync(token, Password, Password)).IsError();
	}

	[Test]
	public async Task JoinAsync_AfterTheInviteWasRevoked_IsRefused()
	{
		var token = await InviteAsync("Mira");
		await Users.RevokeInviteAsync("mira");

		await Assert.That(await Testee.JoinAsync(token, Password, Password)).IsError();
	}

	[Test]
	public async Task JoinAsync_WithTheEarlierLinkOnceANewOneWasMade_IsRefused()
	{
		var earlier = await InviteAsync("Mira");
		await Users.RenewInviteAsync("mira");

		await Assert.That(await Testee.JoinAsync(earlier, Password, Password)).IsError();
	}

	[Test]
	public async Task JoinAsync_WithPasswordsThatDoNotMatch_SaysSoAndKeepsTheInvite()
	{
		var token = await InviteAsync("Mira");

		var mismatched = await Testee.JoinAsync(token, Password, NewPassword);

		await Assert.That(mismatched).IsError().And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("The two passwords do not match."));
		await Assert.That(await Testee.JoinAsync(token, Password, Password)).IsSuccess();
	}

	[Test]
	public async Task ResetPasswordAsync_SetsTheNewPasswordOnceAndEndsTheirSessions()
	{
		var guest = await host.SeedLocalUser("guest", Password, UserRole.Member);
		var token = TokenOf(await Users.ResetLinkAsync(guest));

		var reset = await Testee.ResetPasswordAsync(token, NewPassword, NewPassword);

		await Assert.That(reset).IsSuccess();
		await Assert.That(await SignIn.SignInWithPasswordAsync("guest", NewPassword)).IsSuccess();
		await Assert.That(await SignIn.SignInWithPasswordAsync("guest", Password)).IsError();
		await Assert.That(host.InvalidatedSessions).Contains(guest);
		await Assert.That(await Testee.ResetPasswordAsync(token, Password, Password)).IsError();
	}

	[Test]
	public async Task ResetPasswordAsync_WithTheEarlierLinkOnceANewOneWasMade_IsRefused()
	{
		var guest = await host.SeedLocalUser("guest", Password, UserRole.Member);
		var earlier = TokenOf(await Users.ResetLinkAsync(guest));
		await Users.ResetLinkAsync(guest);

		await Assert.That(await Testee.ResetPasswordAsync(earlier, NewPassword, NewPassword)).IsError();
	}

	[Test]
	public async Task InspectAsync_ForAnInviteOnTheResetPage_FindsNothing()
	{
		var token = await InviteAsync("Mira");

		await Assert.That(await Testee.InspectAsync(AccountLinkKind.Reset, token)).IsNone();
		await Assert.That(await Testee.InspectAsync(AccountLinkKind.Invite, token)).IsSome();
	}

	[Test]
	public async Task JoinAsync_OnceTheInstallSignsInWithPlex_IsRefused()
	{
		var token = await InviteAsync("Mira");
		var config = host.Resolve<IProviderConfigService>();
		await config.StoreAsync((await config.GetAsync()) with { AuthProvider = AuthProvider.Plex });

		await Assert.That(await Testee.JoinAsync(token, Password, Password)).IsError();
	}

	private static string Kind(Person person)
	{
		return person switch
		{
			JoinedPerson => "joined",
			InvitedPerson => "invited",
		};
	}

	private async Task<string> InviteAsync(string username) => TokenOf(await Users.InviteAsync(username));

	private static string TokenOf(Result<IssuedLink> issued)
	{
		return issued.GetOrThrow(error => new InvalidOperationException(error.Reason)).Path.Split('/')[^1];
	}
}
