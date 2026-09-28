using UI.Features.Feeds.Pages;

namespace UI.Tests.Feeds;

public class FeedPagingTests
{
	[Test]
	public async Task PageCount_WhenThereAreNoFeeds_IsStillOnePage()
	{
		await Assert.That(FeedPaging.PageCount(0)).IsEqualTo(1);
	}

	[Test]
	[Arguments(11, 1)]
	[Arguments(12, 2)]
	[Arguments(22, 2)]
	[Arguments(23, 3)]
	public async Task PageCount_FitsElevenFeedsOnEachPage(int feedCount, int expected)
	{
		await Assert.That(FeedPaging.PageCount(feedCount)).IsEqualTo(expected);
	}

	[Test]
	public async Task PageWithin_WhenTheLastFeedOnTheLastPageIsDeleted_FallsBackToThePageThatStillExists()
	{
		await Assert.That(FeedPaging.PageWithin(page: 2, feedCount: 11)).IsEqualTo(1);
	}

	[Test]
	public async Task Slice_TakesTheFeedsBelongingToThatPage()
	{
		var feeds = Enumerable.Range(1, 12).ToList();

		await Assert.That(FeedPaging.Slice(feeds, 2)).IsEquivalentTo([12]);
	}
}
