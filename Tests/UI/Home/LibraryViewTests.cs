using Domain.Feed;
using TestInfrastructure;
using UI.Features.Home.Components;

namespace UI.Tests.Home;

public class LibraryViewTests
{
	[Test]
	public async Task Sources_AttributesWhatIsLeftOverToEverythingThatIsNotAFeed()
	{
		var shares = LibraryView.Sources([Overview("docs", 842), Overview("trending", 331)], total: 1282);

		await Assert.That(shares.Select(share => share.Name)).IsEquivalentTo(new[] { "docs", "trending", "Added by hand or from Plex" });
		await Assert.That(shares[2].Count).IsEqualTo(109);
	}

	[Test]
	public async Task Sources_OrdersTheBiggestContributorFirst()
	{
		var shares = LibraryView.Sources([Overview("docs", 12), Overview("trending", 400)], total: 412);

		await Assert.That(shares[0].Name).IsEqualTo("trending");
		await Assert.That(shares[0].Count).IsEqualTo(400);
	}

	[Test]
	public async Task Sources_WhenEveryTitleCameFromAFeed_LeavesOutTheLeftoverRow()
	{
		var shares = LibraryView.Sources([Overview("docs", 50)], total: 50);

		await Assert.That(shares).HasSingleItem();
	}

	[Test]
	public async Task Sources_ForAFeedWithNothingOnTheWantlist_LeavesItOut()
	{
		var shares = LibraryView.Sources([Overview("docs", 50), Overview("trending", 0)], total: 50);

		await Assert.That(shares.Select(share => share.Name)).IsEquivalentTo(new[] { "docs" });
	}

	[Test]
	public async Task Sources_OnAnEmptyWantlist_ShowsNothingRatherThanZeroRows()
	{
		await Assert.That(LibraryView.Sources([], total: 0)).IsEmpty();
	}

	private static FeedOverview Overview(string name, int onWantlist) => FeedOverview.Of(TestFeeds.Feed(Guid.NewGuid(), name), new None(), onWantlist);
}
