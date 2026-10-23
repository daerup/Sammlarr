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
	private static readonly Secret Password = new("correct horse battery");

	private static readonly Secret Reset = new("a fresh start for once");

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
		await host.Resolve<ILocalAccountRepository>().ChangePasswordAsync(_owner, Reset, new Secret("chosen since then"));

		await Testee(Reset).StartAsync(CancellationToken.None);

		await Assert.That(await SignIn.SignInWithPasswordAsync("owner", new Secret("chosen since then"))).IsSuccess();
	}

	[Test]
	public async Task StartAsync_ForALockedOutAdmin_LiftsTheLock()
	{
		for (var attempt = 0; attempt < 5; attempt++)
		{
			await SignIn.SignInWithPasswordAsync("owner", new Secret("wrong password!"));
		}

		await Testee(Reset).StartAsync(CancellationToken.None);

		await Assert.That(await SignIn.SignInWithPasswordAsync("owner", Reset)).IsSuccess();
	}

	[Test]
	public async Task StartAsync_WithATooShortValue_ChangesNothing()
	{
		await Testee(new Secret("too short")).StartAsync(CancellationToken.None);

		await Assert.That(await SignIn.SignInWithPasswordAsync("owner", Password)).IsSuccess();
	}

	[Test]
	public async Task StartAsync_OnAPlexInstall_ChangesNothing()
	{
		var config = host.Resolve<IProviderConfigService>();
		await config.StoreAsync(await config.GetAsync() with { AuthProvider = AuthProvider.Plex });

		await Testee(Reset).StartAsync(CancellationToken.None);

		var attempt = await host.Resolve<ILocalAccountRepository>().CheckPasswordAsync("owner", Password);
		await Assert.That(attempt).IsOfUnionType<PasswordAttempt, PasswordAccepted>();
	}

	private AdminPasswordReset Testee(Secret value)
	{
		return new AdminPasswordReset(
			host.Resolve<IServiceProvider>(),
			new AccountsOptions { ResetAdminPassword = value },
			NullLogger<AdminPasswordReset>.Instance
		);
	}
}
