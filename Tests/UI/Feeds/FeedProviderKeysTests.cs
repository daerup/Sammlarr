using Contracts.Auth;
using Domain.Auth;
using Domain.Providers;
using UI.Features.Feeds.Pages.Editor;

namespace UI.Tests.Feeds;

public class FeedProviderKeysTests
{
	[Test]
	public async Task WithoutKeyAsync_WithOnlyAnMdblistKey_ListsTheFeedProvidersThatNeedTheOthers()
	{
		var keys = IApiKeyService.Mock();
		keys.GetAsync(ApiKeyProvider.Mdblist, Any()).Returns(Success.Of(IApiKey.Mock().Object));
		keys.GetAsync(ApiKeyProvider.Tmdb, Any()).Returns(Error.From("No API key is configured for this provider."));

		await Assert.That(await FeedProviderKeys.WithoutKeyAsync(keys.Object)).IsEquivalentTo([FeedProvider.Tmdb]);
	}
}
