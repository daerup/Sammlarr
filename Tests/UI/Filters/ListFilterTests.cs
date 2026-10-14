using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using TestInfrastructure;
using TUnit.Assertions.Enums;
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
	public async Task ActiveCount_WithASearchTerm_CountsItAlongsideTheOtherFilters()
	{
		var filter = new ListFilter { Type = MediaType.Movie, Search = "dune" };

		await Assert.That(filter.ActiveCount).IsEqualTo(2);
	}

	[Test]
	[Arguments("dune", "Dune: Part Two", true)]
	[Arguments("DUNE", "Dune: Part Two", true)]
	[Arguments("part two", "Dune: Part Two", true)]
	[Arguments("  dune  ", "Dune: Part Two", true)]
	[Arguments("arrival", "Dune: Part Two", false)]
	public async Task MatchesSearch_AgainstATitle_IgnoresCaseAndSurroundingSpace(string search, string title, bool expected)
	{
		var filter = new ListFilter { Search = search };

		await Assert.That(filter.MatchesSearch(title)).IsEqualTo(expected);
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task MatchesSearch_WithoutATerm_KeepsEveryTitle(string? search)
	{
		var filter = new ListFilter { Search = search };

		await Assert.That(filter.MatchesSearch("anything at all")).IsTrue();
	}

	[Test]
	public async Task ClearFilters_WithASearchTerm_DropsItButKeepsTheSort()
	{
		var filter = new ListFilter
		{
			Search = "dune",
			Type = MediaType.Movie,
			Sort = ListSort.TitleAscending,
		};

		var cleared = filter.ClearFilters();

		await Assert.That(cleared.Search).IsNull();
		await Assert.That(cleared.Type).IsNull();
		await Assert.That(cleared.Sort).IsEqualTo(ListSort.TitleAscending);
	}

	[Test]
	public async Task MatchesSource_WithSeveralSourcesChosen_AcceptsAnyOfThem()
	{
		var other = FeedId.From(new Guid(32, 0, 0, new byte[8]));
		var filter = new ListFilter { Sources = [new SourceChoice(SourceKind.Feed, Feed), new SourceChoice(SourceKind.Manual)] };

		await Assert.That(filter.MatchesSource(TestFeeds.Origin(FeedGuid, Guid.NewGuid()))).IsTrue();
		await Assert.That(filter.MatchesSource(new ManualOrigin(ManualSource.Search))).IsTrue();
		await Assert.That(filter.MatchesSource(TestFeeds.Origin(other.Value, Guid.NewGuid()))).IsFalse();
	}

	[Test]
	public async Task None_WithNothingChosen_AcceptsAnyTypeAndSourceAndCountsNoFilter()
	{
		await Assert.That(ListFilter.None.MatchesType(MediaType.Show)).IsTrue();
		await Assert.That(ListFilter.None.MatchesSource(new ManualOrigin(ManualSource.Browse))).IsTrue();
		await Assert.That(ListFilter.None.MatchesSource(new SinkOrigin(SinkKind.Plex))).IsTrue();
		await Assert.That(ListFilter.None.ActiveCount).IsEqualTo(0);
	}

	[Test]
	public async Task ClearFilters_DropsEveryFilterButKeepsTheChosenOrder()
	{
		var filter = new ListFilter
		{
			Type = MediaType.Movie,
			Sources = [new SourceChoice(SourceKind.Manual)],
			Sort = ListSort.TitleDescending,
		};

		var cleared = filter.ClearFilters();

		await Assert.That(cleared.HasFilters).IsFalse();
		await Assert.That(cleared.Sources).IsEmpty();
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
	[Arguments(SourceKind.Sink, false)]
	[Arguments(SourceKind.Orphaned, false)]
	public async Task MatchesSource_ForNonFeedKinds_MatchesOnlyItsOwnOrigin(SourceKind kind, bool expected)
	{
		var matches = new SourceChoice(kind).Matches(new ManualOrigin(ManualSource.Browse));

		await Assert.That(matches).IsEqualTo(expected);
	}

	[Test]
	public async Task SortBy_TitleAscending_OrdersByTitleRegardlessOfWhenItWasAdded()
	{
		var items = new[] { ("Zodiac", 3), ("Arrival", 1), ("Moana", 2) };

		var sorted = new ListFilter { Sort = ListSort.TitleAscending }
			.SortBy(items, item => Movie(item.Item1), item => DateTimeOffset.UnixEpoch.AddDays(item.Item2))
			.Select(item => item.Item1)
			.ToList();

		await Assert.That(sorted).IsEquivalentTo(["Arrival", "Moana", "Zodiac"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task SortBy_RecentlyAdded_PutsTheNewestFirst()
	{
		var items = new[] { ("Zodiac", 3), ("Arrival", 1), ("Moana", 2) };

		var sorted = new ListFilter()
			.SortBy(items, item => Movie(item.Item1), item => DateTimeOffset.UnixEpoch.AddDays(item.Item2))
			.Select(item => item.Item1)
			.ToList();

		await Assert.That(sorted).IsEquivalentTo(["Zodiac", "Moana", "Arrival"], CollectionOrdering.Matching);
	}

	private static MatchedMedia Movie(string title)
	{
		return new MatchedMedia(
			new MediaIdentity(MediaId.New(), [TmdbId.From(title, MediaType.Movie)]),
			new MovieDetails
			{
				Title = title,
				ReleaseDate = new DateOnly(2020, 1, 1),
				Genres = [],
				Runtime = TimeSpan.FromMinutes(90),
				Ratings = MediaRatings.None,
			}
		);
	}
}
