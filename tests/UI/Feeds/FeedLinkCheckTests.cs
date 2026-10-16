using Contracts.Feed;
using Domain.Feed;
using Domain.Usage;
using Observability.Usage;
using TestInfrastructure;
using UI.Features.Feeds.Pages.Editor;

namespace UI.Tests.Feeds;

public class FeedLinkCheckTests
{
	[Test]
	public async Task CheckLinkAsync_AsksTheProviderAsTheFeedEditorForItsFeed()
	{
		UsageMark mark = default;
		var feed = FeedId.From(Guid.NewGuid());
		var provider = IFeedProvider.Mock();
		provider.NormalizeAsync(Any(), Any()).Returns(Success.Of("users/ana/watchlist")).Callback(() => mark = UsageCauses.Current);

		var answer = await provider.Object.CheckLinkAsync("ana", feed);

		await Assert.That(mark).IsEqualTo(new UsageMark(UsageCause.FeedEditor, Some.Of(feed)));
		await Assert.That(answer).IsSuccess().And.IsEqualTo("users/ana/watchlist");
	}
}
