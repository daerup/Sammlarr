using Domain.Feed;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Domain.Watchlist;
using TestInfrastructure;
using UI.Shared.Filters;

namespace UI.Tests.Filters;

public class ListFilterTests
{
	private static readonly Guid FeedGuid = new(31, 0, 0, new byte[8]);
	private static readonly FeedId Feed = FeedId.From(FeedGuid);

	[Test]
	public async Task ActiveCount_CountsOnlyFiltersSoSortingNeverLooksLikeNarrowing()
	{
		var filter = new ListFilter { Type = MediaType.Movie, Sort = ListSort.TitleAscending };

		await Assert.That(filter.ActiveCount).IsEqualTo(1);
		await Assert.That(filter.HasFilters).IsTrue();
	}

	[Test]
	public async Task ClearFilters_DropsEveryFilterButKeepsTheChosenOrder()
	{
		var filter = new ListFilter
		{
			Type = MediaType.Movie,
			Source = new SourceChoice(SourceKind.Manual),
			Sort = ListSort.TitleDescending,
		};

		var cleared = filter.ClearFilters();

		await Assert.That(cleared.HasFilters).IsFalse();
		await Assert.That(cleared.Sort).IsEqualTo(ListSort.TitleDescending);
	}

	[Test]
	public async Task IsDefault_IsTrueOnlyWithNoFiltersAndTheDefaultOrder()
	{
		await Assert.That(ListFilter.None.IsDefault).IsTrue();
		await Assert.That(new ListFilter { Sort = ListSort.OldestAdded }.IsDefault).IsFalse();
	}

	[Test]
	public async Task MatchesSource_ForAFeedChoice_AcceptsOnlyThatFeed()
	{
		var choice = new SourceChoice(SourceKind.Feed, Feed);
		var other = FeedId.From(new Guid(32, 0, 0, new byte[8]));

		await Assert.That(choice.Matches(TestFeeds.Origin(FeedGuid, Guid.NewGuid()))).IsTrue();
		await Assert.That(choice.Matches(TestFeeds.Origin(other.Value, Guid.NewGuid()))).IsFalse();
		await Assert.That(choice.Matches(new ManualOrigin(ManualSource.Search))).IsFalse();
	}

	[Test]
	[Arguments(SourceKind.Manual, true)]
	[Arguments(SourceKind.Remote, false)]
	[Arguments(SourceKind.Orphaned, false)]
	public async Task MatchesSource_ForNonFeedKinds_MatchesOnlyItsOwnOrigin(SourceKind kind, bool expected)
	{
		var matches = new SourceChoice(kind).Matches(new ManualOrigin(ManualSource.Browse));

		await Assert.That(matches).IsEqualTo(expected);
	}

	[Test]
	public async Task MatchesType_AndMatchesSource_AcceptEverythingWhenUnset()
	{
		await Assert.That(ListFilter.None.MatchesType(MediaType.Show)).IsTrue();
		await Assert.That(ListFilter.None.MatchesSource(new RemoteOrigin(WatchlistProvider.Plex))).IsTrue();
	}

	[Test]
	public async Task SortBy_TitleAscending_OrdersByTitleRegardlessOfWhenItWasAdded()
	{
		var items = new[] { ("Zodiac", 3), ("Arrival", 1), ("Moana", 2) };

		var sorted = new ListFilter { Sort = ListSort.TitleAscending }
			.SortBy(items, item => Movie(item.Item1), item => DateTimeOffset.UnixEpoch.AddDays(item.Item2))
			.Select(item => item.Item1)
			.ToList();

		await Assert.That(sorted).IsEquivalentTo(["Arrival", "Moana", "Zodiac"]);
	}

	[Test]
	public async Task SortBy_RecentlyAdded_PutsTheNewestFirst()
	{
		var items = new[] { ("Zodiac", 3), ("Arrival", 1), ("Moana", 2) };

		var sorted = new ListFilter()
			.SortBy(items, item => Movie(item.Item1), item => DateTimeOffset.UnixEpoch.AddDays(item.Item2))
			.Select(item => item.Item1)
			.ToList();

		await Assert.That(sorted).IsEquivalentTo(["Zodiac", "Moana", "Arrival"]);
	}

	private static IdentifiedMedia Movie(string title) =>
		new PartialMovie
		{
			Identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), [TmdbId.From(title, MediaType.Movie)]),
			Title = title,
			Tagline = string.Empty,
			Summary = string.Empty,
			PosterUrl = string.Empty,
			BackdropUrl = string.Empty,
			ReleaseDate = new DateOnly(2020, 1, 1),
			Genres = [],
			Runtime = TimeSpan.FromMinutes(90),
			Rating = RatingSummary.None,
		};
}
