using Contracts.Auth;
using Contracts.Identity;
using Contracts.Settings;
using Core.Auth;
using Domain.Identity;
using Domain.Providers;
using Harness;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Options;
using TestInfrastructure;

namespace Integration.Tests.Auth;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class AdminPasswordResetTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Password = "correct horse battery";

	private const string Reset = "a fresh start for once";

	private UserId _owner;

	private ISignInService SignIn => host.Resolve<ISignInService>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Before(Test)]
	public async Task Background()
	{
		await host.SignsInWithSammlarrAccounts();
		_owner = await host.SeedLocalUser("owner", Password, UserRole.Admin);
	}

	[Test]
	public async Task StartAsync_OnASammlarrAccountsInstall_ResetsTheAdminsPassword()
	{
		await Testee(Reset).StartAsync(CancellationToken.None);

		await Assert.That(await SignIn.SignInWithPasswordAsync("owner", Reset)).IsSuccess();
	}

	[Test]
	public async Task StartAsync_AgainAfterTheAdminChoseANewPassword_KeepsTheirChoice()
	{
		await Testee(Reset).StartAsync(CancellationToken.None);
		await host.Resolve<ILocalAccountRepository>().SetPasswordAsync(_owner, Passwords.Hash("chosen since then"), DateTimeOffset.UnixEpoch);

		await Testee(Reset).StartAsync(CancellationToken.None);

		await Assert.That(await SignIn.SignInWithPasswordAsync("owner", "chosen since then")).IsSuccess();
	}

	[Test]
	public async Task StartAsync_WithATooShortValue_ChangesNothing()
	{
		await Testee("too short").StartAsync(CancellationToken.None);

		await Assert.That(await SignIn.SignInWithPasswordAsync("owner", Password)).IsSuccess();
	}

	[Test]
	public async Task StartAsync_OnAPlexInstall_ChangesNothing()
	{
		var config = host.Resolve<IProviderConfigService>();
		await config.StoreAsync(await config.GetAsync() with { AuthProvider = AuthProvider.Plex });

		await Testee(Reset).StartAsync(CancellationToken.None);

		var account = await host.Resolve<ILocalAccountRepository>().GetAsync(_owner);
		await Assert.That(account.Match(found => found.PasswordHash.Match(hash => Passwords.Verify(hash, Password), () => false), () => false)).IsTrue();
	}

	private AdminPasswordReset Testee(string value)
	{
		return new AdminPasswordReset(
			host.Resolve<IServiceProvider>(),
			new AccountsOptions { ResetAdminPassword = value },
			NullLogger<AdminPasswordReset>.Instance
		);
	}
}
