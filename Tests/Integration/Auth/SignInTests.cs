using Contracts.Auth;
using Contracts.Identity;
using Domain.Identity;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Auth;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class SignInTests(SammlarrTestHost host) : IAsyncDisposable
{
	private ISignInService Testee => host.Resolve<ISignInService>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task SignInAsync_WhenItIsTheFirstUser_MakesThemAnAdmin()
	{
		await host.NoUsers();
		host.OAuthReturns("plex-uuid", "John");

		var result = await Testee.CompleteAsync(AuthProvider.Plex, "callback");

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

		var result = await Testee.CompleteAsync(AuthProvider.Plex, "callback");

		await Assert.That(result).IsSuccess().And.Multiple(async user => await Assert.That(user.Role).IsEqualTo(UserRole.Admin));
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

		var result = await Testee.CompleteAsync(AuthProvider.Plex, "callback");

		await Assert.That(result).IsSuccess().And.Multiple(async user => await Assert.That(await host.HasStoredCredential(AuthProvider.Plex, user.Id)).IsTrue());
	}

	[Test]
	public async Task SignInAsync_ForEachNewPerson_TakesTheNextColourRoundTheWheel()
	{
		await host.NoUsers();
		host.OAuthReturns("plex-1", "Ada");
		var first = (await Testee.CompleteAsync(AuthProvider.Plex, "callback")).GetOrThrow(error => new InvalidOperationException(error.Reason));
		host.OAuthReturns("plex-2", "Bob");
		var second = (await Testee.CompleteAsync(AuthProvider.Plex, "callback")).GetOrThrow(error => new InvalidOperationException(error.Reason));

		var hues = await host.Resolve<IUserRepository>().GetHuesAsync();

		await Assert.That(hues[first.Id]).IsEqualTo(40);
		await Assert.That(hues[second.Id]).IsEqualTo(177.5);
	}
}
