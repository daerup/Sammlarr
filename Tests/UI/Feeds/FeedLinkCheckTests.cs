using Contracts.Feed;
using Domain.Usage;
using Observability.Usage;
using TestInfrastructure;
using UI.Features.Feeds.Pages.Editor;

namespace UI.Tests.Feeds;

public class FeedLinkCheckTests
{
	[Test]
	public async Task CheckLinkAsync_AsksTheProviderUnderTheFeedEditorCause()
	{
		var cause = UsageCause.Other;
		var provider = IFeedProvider.Mock();
		provider.NormalizeAsync(Any(), Any()).Returns(Success.Of("users/ana/watchlist")).Callback(() => cause = UsageCauses.Current.Cause);

		var answer = await provider.Object.CheckLinkAsync("ana");

		await Assert.That(cause).IsEqualTo(UsageCause.FeedEditor);
		await Assert.That(answer).IsSuccess().And.IsEqualTo("users/ana/watchlist");
	}
}
