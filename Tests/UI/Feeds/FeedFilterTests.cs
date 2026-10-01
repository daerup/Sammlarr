using Domain.Providers;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Pages;
using static UI.Tests.Feeds.TestFeedRows;

namespace UI.Tests.Feeds;

public class FeedFilterTests
{
	[Test]
	public async Task Apply_WithoutFilters_KeepsEveryFeedSortedByName()
	{
		var shown = FeedFilter.None.Apply(All);

		await Assert.That(shown).IsEquivalentTo([Comedy, Documentaries, Trending, WeeklyPicks], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Apply_ForAProvider_KeepsOnlyItsFeeds()
	{
		var shown = (FeedFilter.None with { Providers = [FeedProvider.Tmdb] }).Apply(All);

		await Assert.That(shown).IsEquivalentTo([WeeklyPicks]);
	}

	[Test]
	public async Task Apply_ForBusyFeeds_KeepsTheRunningAndTheQueuedOnes()
	{
		var shown = (FeedFilter.None with { Statuses = [FeedStatus.Busy] }).Apply(All);

		await Assert.That(shown).IsEquivalentTo([Documentaries, Trending]);
	}

	[Test]
	public async Task Apply_AcrossDimensions_KeepsOnlyFeedsMatchingAll()
	{
		var filter = FeedFilter.None with { Triggers = [FeedTriggerKind.Scheduled], Strategies = [FeedStrategyKind.Mirror] };

		await Assert.That(filter.Apply(All)).IsEquivalentTo([Comedy]);
	}

	[Test]
	public async Task Apply_WithASearch_MatchesTheListIdentifierToo()
	{
		var shown = (FeedFilter.None with { Search = "8305" }).Apply(All);

		await Assert.That(shown).IsEquivalentTo([WeeklyPicks]);
	}

	[Test]
	public async Task Apply_SortedByNextRun_PutsFeedsWithoutOneLast()
	{
		var shown = (FeedFilter.None with { Sort = FeedSort.NextRun }).Apply(All);

		await Assert.That(shown.Take(2)).IsEquivalentTo([Comedy, Trending], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Apply_SortedByRecentlyRun_PutsTheNewestRunFirstAndFeedsThatNeverRanLast()
	{
		var older = Ran(Comedy, Now.AddDays(-2));
		var newer = Ran(Trending, Now.AddHours(-1));

		var shown = (FeedFilter.None with { Sort = FeedSort.RecentlyRun }).Apply([Documentaries, older, newer]);

		await Assert.That(shown).IsEquivalentTo([newer, older, Documentaries], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Apply_SortedByMostOnWantlist_PutsTheBiggestCountFirstAndBreaksTiesByName()
	{
		var shown = (FeedFilter.None with { Sort = FeedSort.MostOnWantlist }).Apply(All);

		await Assert.That(shown).IsEquivalentTo([Trending, Comedy, Documentaries, WeeklyPicks], CollectionOrdering.Matching);
	}

	[Test]
	public async Task ClearFilters_KeepsTheChosenSort()
	{
		var filter = FeedFilter.None with { Providers = [FeedProvider.Tmdb], Search = "picks", Sort = FeedSort.RecentlyAdded };

		var cleared = filter.ClearFilters();

		await Assert.That(filter.ActiveCount).IsEqualTo(2);
		await Assert.That(cleared.HasFilters).IsFalse();
		await Assert.That(cleared.Sort).IsEqualTo(FeedSort.RecentlyAdded);
	}

	[Test]
	public async Task Next_SkipsFeedsThatAreAlreadyRunningOrQueued()
	{
		await Assert.That(FeedPulse.Next(All)).IsEqualTo(Comedy);
	}

	[Test]
	public async Task Providers_OffersOnlyTheProvidersThatHaveAFeed()
	{
		var offered = FeedPulse.Providers([Comedy, Documentaries, Trending]);

		await Assert.That(offered).IsEquivalentTo([new ProviderCount(FeedProvider.Mdblist, 3)]);
	}

	[Test]
	public async Task Running_AndQueued_CountTheQueueStatesApart()
	{
		await Assert.That(FeedPulse.Running(All)).IsEqualTo(1);
		await Assert.That(FeedPulse.Queued(All)).IsEqualTo(1);
	}

	private static FeedRow Ran(FeedRow row, DateTimeOffset startedAt) =>
		row with
		{
			Overview = row.Overview with { LastRun = Some.Of(TestFeeds.Run(row.Feed, Guid.NewGuid()) with { StartedAt = startedAt }) },
		};
}
