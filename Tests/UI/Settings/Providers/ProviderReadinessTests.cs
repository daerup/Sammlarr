using Contracts.Auth;
using Domain.Auth;
using Domain.Providers;
using UI.Shared.Services;

namespace UI.Tests.Settings.Providers;

public class ProviderReadinessTests
{
	[Test]
	public async Task GetReadyAsync_WithOnlyAnMdblistKey_ListsMdblist()
	{
		var keys = IApiKeyService.Mock();
		keys.GetAsync(ApiKeyProvider.Mdblist, Any()).Returns(Success.Of(IApiKey.Mock().Object));
		keys.GetAsync(ApiKeyProvider.Tmdb, Any()).Returns(Error.From("No API key is configured for this provider."));

		await Assert.That(await keys.Object.GetReadyAsync()).IsEquivalentTo([ApiKeyProvider.Mdblist]);
	}

	[Test]
	public async Task GetReadyAsync_WithAPlexCredential_ListsPlex()
	{
		var credentials = ICredentialService.Mock();
		credentials.GetAsync(AuthProvider.Plex, Any()).Returns(Success.Of(ICredential.Mock().Object));

		await Assert.That(await credentials.Object.GetReadyAsync()).IsEquivalentTo([AuthProvider.Plex]);
	}
}
