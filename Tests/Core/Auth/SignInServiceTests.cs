using Contracts.Auth;
using Contracts.DI;
using Contracts.Identity;
using Core.Auth;
using Core.Identity;
using Domain.Identity;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace Core.Tests.Auth;

public class SignInServiceTests
{
	private static readonly OAuthTokenGrant Grant = new("plex-token", new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), Guid.NewGuid());

	private static SignInService CreateTestee(Mock<IOAuthProvider> provider, Mock<ICredentialRepository> credentials, Mock<IUserRepository> users) =>
		new(
			new FakeKeyedServiceIndex<AuthProvider, IOAuthProvider>().Register(AuthProvider.Plex, provider.Object),
			credentials.Object,
			users.Object,
			new UserScope(new CurrentUserOverride(), users.Object),
			NullLogger<SignInService>.Instance
		);

	[Test]
	public async Task BeginAsync_DelegatesToConfiguredProvider()
	{
		var provider = IOAuthProvider.Mock();
		provider.BeginLoginAsync("https://callback", Any()).Returns("https://auth-url");
		var testee = CreateTestee(provider, ICredentialRepository.Mock(), IUserRepository.Mock());

		var result = await testee.BeginAsync(AuthProvider.Plex, "https://callback");

		await Assert.That(result).IsEqualTo("https://auth-url");
	}

	[Test]
	public async Task CompleteAsync_WhenFirstUser_StoresCredentialAndProvisionsOwner()
	{
		var provider = IOAuthProvider.Mock();
		provider.CompleteLoginAsync("q", Any()).Returns(Success.Of(new ExternalLogin("plex-uuid", "John", "avatar", Grant)));
		var credentials = ICredentialRepository.Mock();
		var users = IUserRepository.Mock();
		users.FindByLoginAsync(AuthProvider.Plex, "plex-uuid", Any()).Returns(new None());
		users.AnyAsync(Any()).Returns(false);
		var testee = CreateTestee(provider, credentials, users);

		var result = await testee.CompleteAsync(AuthProvider.Plex, "q");

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async user =>
			{
				await Assert.That(user.DisplayName).IsEqualTo("John");
				await Assert.That(user.AvatarUrl).IsEqualTo("avatar");
				await Assert.That(user.Role).IsEqualTo(UserRole.Owner);
			});
		credentials.StoreAsync(AuthProvider.Plex, Grant.AccessToken, Grant.ExpiresAt, Grant.ClientId, Any()).WasCalled(Times.Once);
		users.CreateAsync(Any<User>(), AuthProvider.Plex, "plex-uuid", Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task CompleteAsync_WhenNotFirstUser_ProvisionsMember()
	{
		var provider = IOAuthProvider.Mock();
		provider.CompleteLoginAsync("q", Any()).Returns(Success.Of(new ExternalLogin("plex-uuid", "Jane", "avatar", Grant)));
		var users = IUserRepository.Mock();
		users.FindByLoginAsync(AuthProvider.Plex, "plex-uuid", Any()).Returns(new None());
		users.AnyAsync(Any()).Returns(true);
		var testee = CreateTestee(provider, ICredentialRepository.Mock(), users);

		var result = await testee.CompleteAsync(AuthProvider.Plex, "q");

		await Assert.That(result).IsSuccess().And.Multiple(async user => await Assert.That(user.Role).IsEqualTo(UserRole.Member));
	}

	[Test]
	public async Task CompleteAsync_WhenUserExists_ReturnsExistingWithoutCreating()
	{
		var existing = new User(UserId.New(), "John", "avatar", UserRole.Owner);
		var provider = IOAuthProvider.Mock();
		provider.CompleteLoginAsync("q", Any()).Returns(Success.Of(new ExternalLogin("plex-uuid", "John", "avatar", Grant)));
		var users = IUserRepository.Mock();
		users.FindByLoginAsync(AuthProvider.Plex, "plex-uuid", Any()).Returns(Some.Of(existing));
		var testee = CreateTestee(provider, ICredentialRepository.Mock(), users);

		var result = await testee.CompleteAsync(AuthProvider.Plex, "q");

		await Assert.That(result).IsSuccess().And.Multiple(async user => await Assert.That(user.Id).IsEqualTo(existing.Id));
		users.CreateAsync(Any<User>(), Any<AuthProvider>(), Any<string>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task CompleteAsync_WhenProviderFails_ReturnsErrorAndStoresNothing()
	{
		var provider = IOAuthProvider.Mock();
		provider.CompleteLoginAsync("q", Any()).Returns(Error.From("Authentication was not completed."));
		var credentials = ICredentialRepository.Mock();
		var users = IUserRepository.Mock();
		var testee = CreateTestee(provider, credentials, users);

		var result = await testee.CompleteAsync(AuthProvider.Plex, "q");

		await Assert.That(result).IsError();
		credentials.StoreAsync(Any<AuthProvider>(), Any<string>(), Any<DateTimeOffset>(), Any<Guid>(), Any()).WasNeverCalled();
		users.CreateAsync(Any<User>(), Any<AuthProvider>(), Any<string>(), Any()).WasNeverCalled();
	}
}
