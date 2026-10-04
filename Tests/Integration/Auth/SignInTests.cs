using Contracts.Auth;
using Contracts.Identity;
using Contracts.Settings;
using Core.Setup;
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

	private string SetupCode => host.Resolve<SetupGate>().Code;

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task SignInAsync_WhenItIsTheFirstUser_MakesThemAnAdmin()
	{
		await host.NoUsers();
		host.OAuthReturns("plex-uuid", "John");

		var result = await Testee.CompleteSetupAsync(AuthProvider.Plex, "callback", SetupCode);

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

		var result = await Testee.CompleteSetupAsync(AuthProvider.Plex, "callback", SetupCode);

		await Assert.That(result).IsSuccess().And.Multiple(async user => await Assert.That(user.Role).IsEqualTo(UserRole.Admin));
	}

	[Test]
	public async Task SignInAsync_ForTheFirstUser_ClosesSignUpsUntilTheAdminOpensThem()
	{
		await host.NoUsers();
		host.OAuthReturns("plex-uuid", "Ada");

		await Testee.CompleteSetupAsync(AuthProvider.Plex, "callback", SetupCode);

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

		var result = await Testee.CompleteSetupAsync(AuthProvider.Plex, "callback", SetupCode);

		await Assert.That(result).IsSuccess().And.Multiple(async user => await Assert.That(await host.HasStoredCredential(AuthProvider.Plex, user.Id)).IsTrue());
	}
}
