using Domain.Feed;
using UI.Features.Home.Components;

namespace UI.Tests.Home;

public class LibraryViewTests
{
	private static readonly FeedId Docs = FeedId.From(new Guid("00000000-0000-0000-0000-0000000000d0"));
	private static readonly FeedId Trending = FeedId.From(new Guid("00000000-0000-0000-0000-0000000000d1"));

	private static readonly Dictionary<FeedId, string> Names = new() { [Docs] = "docs", [Trending] = "trending" };

	[Test]
	public async Task Sources_AttributesWhatIsLeftOverToEverythingThatIsNotAFeed()
	{
		var owned = new Dictionary<FeedId, int> { [Docs] = 842, [Trending] = 331 };

		var shares = LibraryView.Sources(owned, Names, total: 1282);

		await Assert.That(shares.Select(share => share.Name)).IsEquivalentTo(new[] { "docs", "trending", "Added by hand or from Plex" });
		await Assert.That(shares[2].Count).IsEqualTo(109);
	}

	[Test]
	public async Task Sources_OrdersTheBiggestContributorFirst()
	{
		var owned = new Dictionary<FeedId, int> { [Docs] = 12, [Trending] = 400 };

		var shares = LibraryView.Sources(owned, Names, total: 412);

		await Assert.That(shares[0].Name).IsEqualTo("trending");
		await Assert.That(shares[0].Count).IsEqualTo(400);
	}

	[Test]
	public async Task Sources_WhenEveryTitleCameFromAFeed_LeavesOutTheLeftoverRow()
	{
		var owned = new Dictionary<FeedId, int> { [Docs] = 50 };

		var shares = LibraryView.Sources(owned, Names, total: 50);

		await Assert.That(shares).HasSingleItem();
	}

	[Test]
	public async Task Sources_OnAnEmptyWatchlist_ShowsNothingRatherThanZeroRows()
	{
		await Assert.That(LibraryView.Sources(new Dictionary<FeedId, int>(), Names, total: 0)).IsEmpty();
	}

	[Test]
	public async Task Sources_ForAFeedThatWasDeleted_StillCountsItsTitles()
	{
		var owned = new Dictionary<FeedId, int> { [FeedId.From(Guid.NewGuid())] = 20 };

		var shares = LibraryView.Sources(owned, Names, total: 20);

		await Assert.That(shares[0].Name).IsEqualTo("A deleted feed");
	}
}
