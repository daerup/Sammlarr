using Contracts;
using Contracts.Auth;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Core.Auth;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace Core.Tests.Auth;

public class OAuthServiceTests
{
	private static ProviderConfig PlexConfig =>
		new()
		{
			WatchlistProvider = WatchlistProvider.Plex,
			TrendingProvider = MediaRecommendationProvider.Tmdb,
			SearchProvider = MediaSearchProvider.Plex,
			RatingProvider = RatingProvider.Tmdb,
		};

	private static OAuthService CreateTestee(Mock<IOAuthProvider> provider, Mock<IProviderConfigRepository> config, Mock<ICredentialRepository> credentials) =>
		new(
			new FakeKeyedServiceIndex<WatchlistProvider, IOAuthProvider>().Register(WatchlistProvider.Plex, provider.Object),
			config.Object,
			credentials.Object,
			NullLogger<OAuthService>.Instance
		);

	[Test]
	public async Task GetUserAsync_WhenCredentialPresent_DelegatesToConfiguredProvider()
	{
		var user = new AuthorizedUser("John", "avatar", "john@example.com", "Plex Pass", DateTimeOffset.UnixEpoch, "https://account");
		var provider = IOAuthProvider.Mock();
		provider.GetUserAsync(Any()).Returns(user);
		var config = IProviderConfigRepository.Mock();
		config.GetAsync(Any()).Returns(PlexConfig);
		var credentials = ICredentialRepository.Mock();
		credentials.GetAsync(WatchlistProvider.Plex, Any()).Returns(new TestCredential(WatchlistProvider.Plex, Guid.NewGuid(), "token", DateTimeOffset.MaxValue));
		var testee = CreateTestee(provider, config, credentials);

		var result = await testee.GetUserAsync();

		await Assert.That(result is AuthorizedUser resolved && resolved == user).IsTrue();
	}

	[Test]
	public async Task GetUserAsync_WhenNoCredential_ReturnsSignedOutWithoutCallingProvider()
	{
		var provider = IOAuthProvider.Mock();
		var config = IProviderConfigRepository.Mock();
		config.GetAsync(Any()).Returns(PlexConfig);
		var testee = CreateTestee(provider, config, ICredentialRepository.Mock());

		var result = await testee.GetUserAsync();

		await Assert.That(result is SignedOut).IsTrue();
		provider.GetUserAsync(Any()).WasNeverCalled();
	}

	[Test]
	public async Task BeginLoginAsync_DelegatesToConfiguredProvider()
	{
		var provider = IOAuthProvider.Mock();
		provider.BeginLoginAsync("https://callback", Any()).Returns("https://auth-url");
		var config = IProviderConfigRepository.Mock();
		config.GetAsync(Any()).Returns(PlexConfig);
		var testee = CreateTestee(provider, config, ICredentialRepository.Mock());

		var result = await testee.BeginLoginAsync("https://callback");

		await Assert.That(result).IsEqualTo("https://auth-url");
	}

	[Test]
	public async Task CompleteLoginAsync_WhenSucceeds_StoresCredentialForConfiguredProvider()
	{
		const string callback = "?pinId=42&clientId=abc&returnUrl=/home";
		var clientId = Guid.NewGuid();
		var expiresAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
		var provider = IOAuthProvider.Mock();
		provider.CompleteLoginAsync(callback, Any()).Returns(new OAuthTokenGrant("token", expiresAt, clientId));
		var config = IProviderConfigRepository.Mock();
		config.GetAsync(Any()).Returns(PlexConfig);
		var credentials = ICredentialRepository.Mock();
		var testee = CreateTestee(provider, config, credentials);

		var result = await testee.CompleteLoginAsync(callback);

		await Assert.That(result).IsNone();
		credentials.StoreAsync(WatchlistProvider.Plex, "token", expiresAt, clientId, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task CompleteLoginAsync_WhenProviderFails_PropagatesErrorAndStoresNothing()
	{
		const string callback = "?pinId=42&clientId=abc";
		var provider = IOAuthProvider.Mock();
		provider.CompleteLoginAsync(callback, Any()).Returns(Error.From("Authentication was not completed."));
		var config = IProviderConfigRepository.Mock();
		config.GetAsync(Any()).Returns(PlexConfig);
		var credentials = ICredentialRepository.Mock();
		var testee = CreateTestee(provider, config, credentials);

		var result = await testee.CompleteLoginAsync(callback);

		await Assert.That(result).IsSome();
		credentials.StoreAsync(Any(), Any(), Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task LogoutAsync_ClearsCredential()
	{
		var provider = IOAuthProvider.Mock();
		var config = IProviderConfigRepository.Mock();
		config.GetAsync(Any()).Returns(PlexConfig);
		var credentials = ICredentialRepository.Mock();
		var testee = CreateTestee(provider, config, credentials);

		await testee.LogoutAsync();

		credentials.ClearAsync(WatchlistProvider.Plex, Any()).WasCalled(Times.Once);
	}
}
