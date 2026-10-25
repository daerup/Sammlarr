using Contracts.Auth;
using Contracts.Identity;
using Contracts.Settings;
using Core.Setup;
using Domain.Auth;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Auth;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class SignInTests(SammlarrTestHost host) : IAsyncDisposable
{
	private ISignInService Testee => host.Resolve<ISignInService>();

	private Secret SetupCode => host.Resolve<SetupGate>().Code;

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task SignInAsync_WhenItIsTheFirstUser_MakesThemAnAdmin()
	{
		await host.NoUsers();
		host.OAuthReturns("plex-uuid", "John");

		var result = await SetUpAsync();

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async user =>
			{
				await Assert.That(user.DisplayName).IsEqualTo("John");
				await Assert.That(user.Role).IsEqualTo(UserRole.Admin);
			});
	}

	[Test]
	public async Task SignInAsync_WhenALaterUserSignsIn_MakesThemAMember()
	{
		host.OAuthReturns("plex-uuid", "Jane");

		var result = await Testee.CompleteAsync(AuthProvider.Plex, "callback");

		await Assert.That(result).IsSuccess().And.Multiple(async user => await Assert.That(user.Role).IsEqualTo(UserRole.Member));
	}

	[Test]
	public async Task SignInAsync_WhenRegistrationIsClosed_RejectsANewUser()
	{
		await host.RegistrationClosed();
		host.OAuthReturns("plex-uuid", "Zoe");

		var result = await Testee.CompleteAsync(AuthProvider.Plex, "callback");

		await Assert.That(result).IsError();
		await Assert.That(await host.ScopedDbContext.Users.CountAsync()).IsEqualTo(1);
	}

	[Test]
	public async Task SignInAsync_WhenRegistrationIsClosed_StillAdmitsTheFirstUser()
	{
		await host.NoUsers();
		await host.RegistrationClosed();
		host.OAuthReturns("plex-uuid", "Ada");

		var result = await SetUpAsync();

		await Assert.That(result).IsSuccess().And.Multiple(async user => await Assert.That(user.Role).IsEqualTo(UserRole.Admin));
	}

	[Test]
	public async Task SignInAsync_ForTheFirstUser_ClosesSignUpsUntilTheAdminOpensThem()
	{
		await host.NoUsers();
		host.OAuthReturns("plex-uuid", "Ada");

		await SetUpAsync();

		await Assert.That((await host.Resolve<IProviderConfigService>().GetAsync()).RegistrationMode).IsEqualTo(RegistrationMode.Closed);
	}

	[Test]
	public async Task SignInAsync_ForTheFirstUserWithoutTheSetupCode_CreatesNobody()
	{
		await host.NoUsers();
		host.OAuthReturns("plex-uuid", "Mallory");

		var result = await Testee.CompleteAsync(AuthProvider.Plex, "callback");

		await Assert.That(result).IsError();
		await Assert.That(await host.ScopedDbContext.Users.CountAsync()).IsEqualTo(0);
	}

	[Test]
	public async Task SignInAsync_WhenTheLoginAlreadyExists_ReturnsTheSameUser()
	{
		var existing = await host.SeedExistingUser("plex-uuid", UserRole.Admin, "John");
		host.OAuthReturns("plex-uuid", "John");

		var result = await Testee.CompleteAsync(AuthProvider.Plex, "callback");

		await Assert.That(result).IsSuccess().And.Multiple(async user => await Assert.That(user.Id).IsEqualTo(existing));
	}

	[Test]
	public async Task SignInAsync_ASecondTime_FindsTheUserTheFirstSignInCreated()
	{
		var before = await host.ScopedDbContext.Users.CountAsync();
		host.OAuthReturns("plex-new", "Nina");
		await Testee.CompleteAsync(AuthProvider.Plex, "callback");

		var again = await Testee.CompleteAsync(AuthProvider.Plex, "callback");

		await Assert.That(again).IsSuccess();
		await Assert.That(await host.ScopedDbContext.Users.CountAsync()).IsEqualTo(before + 1);
	}

	[Test]
	public async Task SignInAsync_AfterAPlexRename_RefreshesTheNameAndPictureButKeepsTheTagName()
	{
		var existing = await host.SeedExistingUser("plex-uuid", UserRole.Member, "John");
		host.OAuthReturns("plex-uuid", "Johnny", "new-avatar");

		var result = await Testee.CompleteAsync(AuthProvider.Plex, "callback");

		var stored = await host.Resolve<IUserRepository>().GetAsync(existing);
		await Assert.That(result).IsSuccess().And.IsEqualTo(stored);
		await Assert.That((stored.DisplayName, stored.AvatarUrl, stored.TagName)).IsEqualTo(("Johnny", "new-avatar", "John"));
	}

	[Test]
	public async Task SignInAsync_WhenTheProviderLoginFails_ReportsAnError()
	{
		host.OAuthFails("Authentication was not completed.");

		var result = await Testee.CompleteAsync(AuthProvider.Plex, "callback");

		await Assert.That(result).IsError();
	}

	[Test]
	public async Task SignInAsync_WhenItSucceeds_StoresTheProviderCredential()
	{
		await host.NoUsers();
		host.OAuthReturns("plex-uuid", "John");

		var result = await SetUpAsync();

		await Assert.That(result).IsSuccess().And.Multiple(async user => await Assert.That(await host.HasStoredCredential(AuthProvider.Plex, user.Id)).IsTrue());
	}

	[Test]
	public async Task PendAdminAsync_ForTheFirstSignIn_ShowsWhoPlexReturnedAndCreatesNobody()
	{
		await host.NoUsers();
		host.OAuthReturns("plex-uuid", "Mia", home: HomeRole.Managed);

		var pending = await Testee.PendAdminAsync(AuthProvider.Plex, "callback");

		await Assert.That(pending).IsSuccess().And.Multiple(async found => await Assert.That((found.DisplayName, found.Home)).IsEqualTo(("Mia", HomeRole.Managed)));
		await Assert.That(await host.ScopedDbContext.Users.CountAsync()).IsEqualTo(0);
	}

	[Test]
	public async Task ConfirmAdminAsync_WhenTheProviderRefusesTheAccountAsAdmin_SaysWhyAndCreatesNobody()
	{
		await host.NoUsers();
		host.OAuthReturns("plex-uuid", "Mia");
		host.OAuthRefusesAdmin("Not an admin account.");

		var result = await SetUpAsync();

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("Not an admin account."));
		await Assert.That(await host.ScopedDbContext.Users.CountAsync()).IsEqualTo(0);
	}

	[Test]
	public async Task ConfirmAdminAsync_OnceSomeoneExists_RefusesWithoutSigningAnyoneIn()
	{
		var daerup = await host.SeedExistingUser("plex-uuid", UserRole.Member, "Daerup");
		host.OAuthReturns("plex-uuid", "Daerup");
		var id = await PendAsync();

		var result = await Testee.ConfirmAdminAsync(id, SetupCode);

		await Assert
			.That(result)
			.IsError()
			.And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("This Sammlarr already has an admin. Sign in instead."));
		await Assert.That(await host.HasStoredCredential(AuthProvider.Plex, daerup)).IsFalse();
	}

	[Test]
	public async Task ConfirmAdminAsync_OnceConfirmed_ForgetsThePendingLogin()
	{
		await host.NoUsers();
		host.OAuthReturns("plex-uuid", "Daerup", home: HomeRole.Admin);
		var id = await PendAsync();

		await Testee.ConfirmAdminAsync(id, SetupCode);

		await Assert.That(Testee.Pending(id)).IsNone();
	}

	[Test]
	public async Task LinkAsync_OnAPlexInstall_ForTheSignedInPlexAccount_RefreshesItsToken()
	{
		var existing = await host.SeedExistingUser("plex-uuid", UserRole.Admin, "Daerup");
		host.OAuthReturns("plex-uuid", "Daerup");

		var result = await Testee.LinkAsync(AuthProvider.Plex, "callback", existing);

		await Assert.That(result).IsSuccess().And.Multiple(async user => await Assert.That(user.Id).IsEqualTo(existing));
		await Assert.That(await host.HasStoredCredential(AuthProvider.Plex, existing)).IsTrue();
	}

	[Test]
	public async Task LinkAsync_OnAPlexInstall_ForAnotherPlexAccount_RefusesNamingBothAndAddsNobody()
	{
		var existing = await host.SeedExistingUser("plex-uuid", UserRole.Admin, "Daerup");
		var before = await host.ScopedDbContext.Users.CountAsync();
		host.OAuthReturns("plex-mia", "Mia", home: HomeRole.Managed);

		var result = await Testee.LinkAsync(AuthProvider.Plex, "callback", existing);

		await Assert
			.That(result)
			.IsError()
			.And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("Plex returned Mia instead of Daerup. Switch to Daerup in Plex, then reconnect."));
		await Assert.That(await host.ScopedDbContext.Users.CountAsync()).IsEqualTo(before);
	}

	[Test]
	public async Task LinkAsync_OnAPlexInstall_ForAnotherUsersPlexAccount_Refuses()
	{
		var existing = await host.SeedExistingUser("plex-uuid", UserRole.Admin, "Daerup");
		await host.SeedExistingUser("plex-lena", UserRole.Member, "Lena");
		host.OAuthReturns("plex-lena", "Lena");

		var result = await Testee.LinkAsync(AuthProvider.Plex, "callback", existing);

		await Assert.That(result).IsError();
	}

	private async Task<Result<User>> SetUpAsync()
	{
		var pending = await Testee.PendAdminAsync(AuthProvider.Plex, "callback");
		return await pending.Map(found => Testee.ConfirmAdminAsync(found.Id, SetupCode));
	}

	private async Task<Guid> PendAsync()
	{
		var pending = await Assert.That(await Testee.PendAdminAsync(AuthProvider.Plex, "callback")).IsSuccess();
		return pending!.Id;
	}
}
