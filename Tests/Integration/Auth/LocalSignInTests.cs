using Contracts.Auth;
using Contracts.Identity;
using Contracts.Settings;
using Core.Setup;
using Domain.Identity;
using Domain.Providers;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Auth;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class LocalSignInTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Password = "correct horse battery";

	private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

	private ISignInService Testee => host.Resolve<ISignInService>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Before(Test)]
	public async Task Background()
	{
		host.SetCurrentTime(Now);
		await host.SignsInWithSammlarrAccounts();
	}

	[Test]
	public async Task SignInWithPasswordAsync_WithTheRightPassword_SignsTheAccountIn()
	{
		var guest = await host.SeedLocalUser("Guest", Password, UserRole.Member);

		var result = await Testee.SignInWithPasswordAsync(" guest ", Password);

		await Assert.That(result).IsSuccess().And.Multiple(async user => await Assert.That(user.Id).IsEqualTo(guest));
	}

	[Test]
	public async Task SignInWithPasswordAsync_WithAWrongPassword_SaysSoWithoutNamingWhichPart()
	{
		await host.SeedLocalUser("guest", Password, UserRole.Member);

		var result = await Testee.SignInWithPasswordAsync("guest", "wrong password!");

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("Wrong username or password."));
	}

	[Test]
	public async Task SignInWithPasswordAsync_ForAnUnknownUser_SaysTheSameAsAWrongPassword()
	{
		var result = await Testee.SignInWithPasswordAsync("nobody", Password);

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("Wrong username or password."));
	}

	[Test]
	public async Task SignInWithPasswordAsync_AfterFiveWrongPasswords_LocksTheAccountEvenForTheRightOne()
	{
		await host.SeedLocalUser("guest", Password, UserRole.Member);
		for (var attempt = 0; attempt < 5; attempt++)
		{
			await Testee.SignInWithPasswordAsync("guest", "wrong password!");
		}

		var result = await Testee.SignInWithPasswordAsync("guest", Password);

		await Assert
			.That(result)
			.IsError()
			.And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("Too many failed sign-ins. Try again in 15 minutes."));
	}

	[Test]
	public async Task SignInWithPasswordAsync_OnceTheLockRunsOut_SignsInAgain()
	{
		await host.SeedLocalUser("guest", Password, UserRole.Member);
		for (var attempt = 0; attempt < 5; attempt++)
		{
			await Testee.SignInWithPasswordAsync("guest", "wrong password!");
		}

		host.SetCurrentTime(Now.AddMinutes(16));
		var result = await Testee.SignInWithPasswordAsync("guest", Password);

		await Assert.That(result).IsSuccess();
	}

	[Test]
	public async Task SignInWithPasswordAsync_OnAPlexInstall_Refuses()
	{
		await host.SeedLocalUser("guest", Password, UserRole.Member);
		var config = host.Resolve<IProviderConfigService>();
		await config.StoreAsync((await config.GetAsync()) with { AuthProvider = AuthProvider.Plex });

		var result = await Testee.SignInWithPasswordAsync("guest", Password);

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("This Sammlarr signs in with Plex."));
	}

	[Test]
	public async Task CompleteAsync_ForAPlexSignInOnASammlarrAccountsInstall_Refuses()
	{
		host.OAuthReturns("plex-uuid", "Stranger");

		var result = await Testee.CompleteAsync(AuthProvider.Plex, "callback");

		await Assert
			.That(result)
			.IsError()
			.And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("This Sammlarr signs in with Sammlarr accounts."));
	}

	[Test]
	public async Task LinkAsync_ForASignedInSammlarrAccount_StoresThePlexTokenForThemAndAddsNobody()
	{
		var guest = await host.SeedLocalUser("guest", Password, UserRole.Member);
		var before = (await host.Resolve<IUserRepository>().GetAllAsync()).Count;
		host.OAuthReturns("plex-uuid", "Someone on Plex");

		var result = await Testee.LinkAsync(AuthProvider.Plex, "callback", guest);

		await Assert.That(result).IsSuccess().And.Multiple(async user => await Assert.That(user.Id).IsEqualTo(guest));
		await Assert.That((await host.Resolve<IUserRepository>().GetAllAsync()).Count).IsEqualTo(before);
		await Assert.That(await host.HasStoredCredential(AuthProvider.Plex, guest)).IsTrue();
		await Assert.That((await host.Resolve<IUserRepository>().GetAsync(guest)).DisplayName).IsEqualTo("guest");
	}

	[Test]
	public async Task SignInWithPasswordAsync_AfterTheRightPassword_CountsFailuresFromZeroAgain()
	{
		await host.SeedLocalUser("guest", Password, UserRole.Member);
		await WrongPasswordsAsync(4);
		await Testee.SignInWithPasswordAsync("guest", Password);
		await WrongPasswordsAsync(4);

		var result = await Testee.SignInWithPasswordAsync("guest", Password);

		await Assert.That(result).IsSuccess();
	}

	[Test]
	public async Task CompleteSetupAsync_ForTheFirstPlexSignInOnAnEmptyInstall_MakesThemAdminAndSwitchesToPlex()
	{
		await host.NoUsers();
		host.OAuthReturns("plex-uuid", "First");

		var result = await Testee.CompleteSetupAsync(AuthProvider.Plex, "callback", host.Resolve<SetupGate>().Code);

		await Assert.That(result).IsSuccess().And.Multiple(async user => await Assert.That(user.Role).IsEqualTo(UserRole.Admin));
		await Assert.That((await host.Resolve<IProviderConfigService>().GetAsync()).AuthProvider).IsEqualTo(AuthProvider.Plex);
	}

	[Test]
	public async Task LinkAsync_OnAPlexInstall_SignsInAsThePlexAccount()
	{
		var guest = await host.SeedLocalUser("guest", Password, UserRole.Member);
		var config = host.Resolve<IProviderConfigService>();
		await config.StoreAsync((await config.GetAsync()) with { AuthProvider = AuthProvider.Plex });
		host.OAuthReturns("plex-uuid", "Someone on Plex");

		var result = await Testee.LinkAsync(AuthProvider.Plex, "callback", guest);

		await Assert.That(result).IsSuccess().And.Multiple(async user => await Assert.That(user.DisplayName).IsEqualTo("Someone on Plex"));
		await Assert.That(await host.HasStoredCredential(AuthProvider.Plex, guest)).IsFalse();
	}

	private async Task WrongPasswordsAsync(int count)
	{
		for (var attempt = 0; attempt < count; attempt++)
		{
			await Testee.SignInWithPasswordAsync("guest", "wrong password!");
		}
	}
}
