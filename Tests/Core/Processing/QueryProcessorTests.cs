using System.ComponentModel;
using Contracts.Processing;
using Core.Processing;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Processing;
using TUnit.Assertions.Enums;
using static TestInfrastructure.QueryFilterFactory;

namespace Core.Tests.Processing;

public class QueryProcessorTests
{
	private readonly QueryProcessor<Item> _testee = new();

	[Test]
	public async Task Execute_WhenNoOptionsProvided_QualifiesEveryItem()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama") };

		var results = await _testee.ExecuteAsync(items, new QueryOptions(), Enrolled()).ToListAsync();

		await Assert.That(Outcomes(results)).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_WhenLimitProvided_TruncatesPastTheLimit()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama") };
		var options = new QueryOptions { Limit = Some.Of(new QueryLimit { Count = 1 }) };

		var results = await _testee.ExecuteAsync(items, options, Enrolled()).ToListAsync();

		await Assert.That(Outcomes(results)).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Truncated], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_WhenSortProvided_QualifiesEveryItemInSortedOrder()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama") };
		var options = new QueryOptions { Sort = Some.Of(new QuerySort { Property = nameof(Item.Year), Direction = ListSortDirection.Descending }) };

		var results = await _testee.ExecuteAsync(items, options, Enrolled()).ToListAsync();

		await Assert.That(Outcomes(results)).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Qualified], CollectionOrdering.Matching);
		await Assert.That(results.Select(row => row.Item)).IsEquivalentTo([items[1], items[0]], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_WhenSortAndLimitProvided_TruncatesTheLowestRanked()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama"), new Item("C", 2019, "Comedy") };
		var options = new QueryOptions
		{
			Limit = Some.Of(new QueryLimit { Count = 2 }),
			Sort = Some.Of(new QuerySort { Property = nameof(Item.Year), Direction = ListSortDirection.Ascending }),
		};

		var results = await _testee.ExecuteAsync(items, options, Enrolled()).ToListAsync();

		await Assert.That(Outcomes(results)).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Qualified, QueryOutcome.Truncated], CollectionOrdering.Matching);
		await Assert.That(Items(results, QueryOutcome.Qualified)).IsEquivalentTo([items[2], items[0]], CollectionOrdering.Matching);
		await Assert.That(Items(results, QueryOutcome.Truncated)).IsEquivalentTo([items[1]]);
	}

	[Test]
	public async Task Execute_WhenFilterProvided_CullsEverythingThatDoesNotMatch()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama"), new Item("C", 2019, "Comedy") };
		var options = new QueryOptions { Filter = Some.Of(Filter(Group(String("Genre", FilterPredicateOperator.Equals, "Drama")))) };

		var results = await _testee.ExecuteAsync(items, options, Enrolled()).ToListAsync();

		await Assert.That(Outcomes(results)).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_WhenFilterAndLimitProvided_CulledItemsDoNotConsumeTheLimit()
	{
		var items = new[]
		{
			new Item("A", 2021, "Action"),
			new Item("B", 2020, "Drama"),
			new Item("C", 2019, "Comedy"),
			new Item("D", 2014, "Horror"),
			new Item("E", 2016, "Anime"),
		};
		var options = new QueryOptions
		{
			Filter = Some.Of(
				Filter(
					FilterOperator.Or,
					Group(String("Genre", FilterPredicateOperator.Equals, "Drama")),
					Group(Number("Year", FilterPredicateOperator.LessThan, 2017))
				)
			),
			Limit = Some.Of(new QueryLimit { Count = 2 }),
		};

		var results = await _testee.ExecuteAsync(items, options, Enrolled()).ToListAsync();

		await Assert
			.That(Outcomes(results))
			.IsEquivalentTo(
				[QueryOutcome.Culled, QueryOutcome.Qualified, QueryOutcome.Culled, QueryOutcome.Qualified, QueryOutcome.Truncated],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Execute_WhenOrFilterProvided_QualifiesItemsMatchingEitherGroup()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama"), new Item("C", 2019, "Comedy") };
		var options = new QueryOptions
		{
			Filter = Some.Of(
				Filter(
					FilterOperator.Or,
					Group(String("Genre", FilterPredicateOperator.Equals, "Drama")),
					Group(Number("Year", FilterPredicateOperator.LessOrEqual, 2019))
				)
			),
		};

		var results = await _testee.ExecuteAsync(items, options, Enrolled()).ToListAsync();

		await Assert.That(Outcomes(results)).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_WhenWantlistedIsPreserved_TheWantlistedItemDoesNotConsumeASlot()
	{
		var wantlisted = new Item("Wantlisted", 2020, "Action");
		var normal = new Item("Normal", 2021, "Drama");
		var options = new QueryOptions { Limit = Some.Of(new QueryLimit { Count = 1, Wantlisted = SlotPolicy.Preserved }) };

		var results = await _testee.ExecuteAsync([wantlisted, normal], options, Enrolled(wantlisted: wantlisted)).ToListAsync();

		await Assert.That(Outcomes(results)).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Qualified], CollectionOrdering.Matching);
		await Assert.That(results[0].Bypassed).IsTrue();
		await Assert.That(results[0].ConsumesSlot).IsFalse();
	}

	[Test]
	public async Task Execute_WhenBlocklistedIsPreserved_TheBlocklistedItemDoesNotConsumeASlot()
	{
		var blocklisted = new Item("Blocklisted", 2020, "Action");
		var normal = new Item("Normal", 2021, "Drama");
		var options = new QueryOptions { Limit = Some.Of(new QueryLimit { Count = 1, Blocklisted = SlotPolicy.Preserved }) };

		var results = await _testee.ExecuteAsync([blocklisted, normal], options, Enrolled(blocklisted: blocklisted)).ToListAsync();

		await Assert.That(Outcomes(results)).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Qualified], CollectionOrdering.Matching);
		await Assert.That(results[0].Enrollment.InBlocklist).IsTrue();
		await Assert.That(results[0].ConsumesSlot).IsFalse();
	}

	[Test]
	public async Task Execute_WhenPreservedItemsAreMixedWithOthers_OnlyTheOthersConsumeTheLimit()
	{
		var wantlisted = new Item("Wantlisted", 2019, "Action");
		var normal1 = new Item("Normal1", 2020, "Drama");
		var normal2 = new Item("Normal2", 2021, "Comedy");
		var options = new QueryOptions { Limit = Some.Of(new QueryLimit { Count = 1, Wantlisted = SlotPolicy.Preserved }) };

		var results = await _testee.ExecuteAsync([wantlisted, normal1, normal2], options, Enrolled(wantlisted: wantlisted)).ToListAsync();

		await Assert.That(Outcomes(results)).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Qualified, QueryOutcome.Truncated], CollectionOrdering.Matching);
		await Assert.That(results[0].ConsumesSlot).IsFalse();
		await Assert.That(results[1].ConsumesSlot).IsTrue();
	}

	[Test]
	public async Task Execute_WhenTheLimitIsAlreadyReached_PreservedItemsAreStillTruncated()
	{
		var normal1 = new Item("Normal1", 2019, "Action");
		var wantlisted = new Item("Wantlisted", 2020, "Drama");
		var normal2 = new Item("Normal2", 2021, "Comedy");
		var options = new QueryOptions { Limit = Some.Of(new QueryLimit { Count = 1, Wantlisted = SlotPolicy.Preserved }) };

		var results = await _testee.ExecuteAsync([normal1, wantlisted, normal2], options, Enrolled(wantlisted: wantlisted)).ToListAsync();

		await Assert.That(Outcomes(results)).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Truncated, QueryOutcome.Truncated], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_WhenWantlistedIsConsumed_TheWantlistedItemBypassesButConsumesASlot()
	{
		var wantlisted = new Item("Wantlisted", 2020, "Action");
		var normal = new Item("Normal", 2021, "Drama");
		var options = new QueryOptions { Limit = Some.Of(new QueryLimit { Count = 2 }) };

		var results = await _testee.ExecuteAsync([wantlisted, normal], options, Enrolled(wantlisted: wantlisted)).ToListAsync();

		await Assert.That(Outcomes(results)).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Qualified], CollectionOrdering.Matching);
		await Assert.That(results[0].Bypassed).IsTrue();
		await Assert.That(results[0].Enrollment.InWantlist).IsTrue();
		await Assert.That(results[0].ConsumesSlot).IsTrue();
	}

	[Test]
	public async Task Execute_WhenItemIsWantlistedAndBlocklisted_TheWantlistPolicyWins()
	{
		var both = new Item("Both", 2020, "Action");
		var normal = new Item("Normal", 2021, "Drama");
		var options = new QueryOptions
		{
			Limit = Some.Of(
				new QueryLimit
				{
					Count = 1,
					Wantlisted = SlotPolicy.Preserved,
					Blocklisted = SlotPolicy.Consumed,
				}
			),
		};

		var results = await _testee.ExecuteAsync([both, normal], options, Enrolled(wantlisted: both, blocklisted: both)).ToListAsync();

		await Assert.That(Outcomes(results)).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Qualified], CollectionOrdering.Matching);
		await Assert.That(results[0].ConsumesSlot).IsFalse();
	}

	[Test]
	public async Task Execute_WhenItemIsWantlistedAndBlocklisted_TheBlocklistPolicyIsIgnored()
	{
		var both = new Item("Both", 2020, "Action");
		var normal = new Item("Normal", 2021, "Drama");
		var options = new QueryOptions
		{
			Limit = Some.Of(
				new QueryLimit
				{
					Count = 1,
					Wantlisted = SlotPolicy.Consumed,
					Blocklisted = SlotPolicy.Preserved,
				}
			),
		};

		var results = await _testee.ExecuteAsync([both, normal], options, Enrolled(wantlisted: both, blocklisted: both)).ToListAsync();

		await Assert.That(Outcomes(results)).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Truncated], CollectionOrdering.Matching);
		await Assert.That(results[0].ConsumesSlot).IsTrue();
	}

	[Test]
	public async Task Execute_WhenABypassedItemConsumesTheOnlySlot_TheNextItemIsTruncated()
	{
		var wantlisted = new Item("Wantlisted", 2020, "Action");
		var normal = new Item("Normal", 2021, "Drama");
		var options = new QueryOptions { Limit = Some.Of(new QueryLimit { Count = 1 }) };

		var results = await _testee.ExecuteAsync([wantlisted, normal], options, Enrolled(wantlisted: wantlisted)).ToListAsync();

		await Assert.That(Outcomes(results)).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Truncated], CollectionOrdering.Matching);
		await Assert.That(results[0].Bypassed).IsTrue();
	}

	[Test]
	public async Task Execute_WhenAnOwnedItemIsAlsoWantlisted_ItQualifiesAsOwnedAndIsNeverBypassed()
	{
		var owned = new Item("Owned", 2020, "Action");

		var results = await _testee.ExecuteAsync([owned], new QueryOptions(), Enrolled(wantlisted: owned, owned: owned)).ToListAsync();

		await Assert.That(results[0].Outcome).IsEqualTo(QueryOutcome.Qualified);
		await Assert.That(results[0].Enrollment.Owned).IsTrue();
		await Assert.That(results[0].Bypassed).IsFalse();
		await Assert.That(results[0].Kept).IsTrue();
	}

	[Test]
	public async Task Execute_WhenAnOwnedItemIsWantlistedAndPreserved_ItStillConsumesASlot()
	{
		var owned = new Item("Owned", 2020, "Action");
		var normal = new Item("Normal", 2021, "Drama");
		var options = new QueryOptions { Limit = Some.Of(new QueryLimit { Count = 1, Wantlisted = SlotPolicy.Preserved }) };

		var results = await _testee.ExecuteAsync([owned, normal], options, Enrolled(wantlisted: owned, owned: owned)).ToListAsync();

		await Assert.That(results[0].ConsumesSlot).IsTrue();
		await Assert.That(results[1].Outcome).IsEqualTo(QueryOutcome.Truncated);
	}

	[Test]
	public async Task Execute_WhenAnOwnedItemIsPastTheLimit_ItIsTruncatedAndAtRisk()
	{
		var first = new Item("First", 2020, "Action");
		var owned = new Item("Owned", 2021, "Drama");
		var options = new QueryOptions { Limit = Some.Of(new QueryLimit { Count = 1 }) };

		var results = await _testee.ExecuteAsync([first, owned], options, Enrolled(owned: owned)).ToListAsync();

		await Assert.That(results[1].Outcome).IsEqualTo(QueryOutcome.Truncated);
		await Assert.That(results[1].AtRisk).IsTrue();
	}

	[Test]
	public async Task Execute_WhenAnOwnedItemFailsTheFilter_ItIsCulledAndAtRisk()
	{
		var owned = new Item("Owned", 2020, "Action");
		var options = new QueryOptions { Filter = Some.Of(Filter(Group(String("Genre", FilterPredicateOperator.Equals, "Drama")))) };

		var results = await _testee.ExecuteAsync([owned], options, Enrolled(owned: owned)).ToListAsync();

		await Assert.That(results[0].Outcome).IsEqualTo(QueryOutcome.Culled);
		await Assert.That(results[0].AtRisk).IsTrue();
	}

	[Test]
	public async Task Execute_WhenAnOwnedItemIsBlocklisted_TheBlocklistChangesNothingAboutItsOwnership()
	{
		var owned = new Item("Owned", 2020, "Action");
		var predicates = Enrolled(blocklisted: owned, owned: owned);
		var culling = new QueryOptions { Filter = Some.Of(Filter(Group(String("Genre", FilterPredicateOperator.Equals, "Drama")))) };

		var qualified = await _testee.ExecuteAsync([owned], new QueryOptions(), predicates).ToListAsync();
		var culled = await _testee.ExecuteAsync([owned], culling, predicates).ToListAsync();

		await Assert.That(qualified[0].Kept).IsTrue();
		await Assert.That(culled[0].AtRisk).IsTrue();
	}

	private static QueryPredicates<Item> Enrolled(Item? wantlisted = null, Item? blocklisted = null, Item? owned = null) =>
		new(IsWantlisted: item => item == wantlisted, IsBlocklisted: item => item == blocklisted, IsOwned: item => item == owned);

	private static IEnumerable<QueryOutcome> Outcomes(IEnumerable<QueryVerdict<Item>> rows) => rows.Select(row => row.Outcome);

	private static IEnumerable<Item> Items(IEnumerable<QueryVerdict<Item>> rows, QueryOutcome outcome) =>
		rows.Where(row => row.Outcome == outcome).Select(row => row.Item);
}

public class QueryProcessorRatingTests
{
	private readonly QueryProcessor<MediaQueryRow> _testee = new();

	[Test]
	public async Task Execute_WhenSortedByRatingDescending_ItemsWithoutRatingComeLast()
	{
		var rated = MovieWithRating(2020, 8.5f);
		var lowRated = MovieWithRating(2021, 5.0f);
		var unrated = MovieWithRating(2022, null);
		var options = new QueryOptions { Sort = Some.Of(new QuerySort { Property = nameof(MediaQueryRow.Rating), Direction = ListSortDirection.Descending }) };

		var results = await _testee.ExecuteAsync([lowRated, unrated, rated], options, NoEnrollment).ToListAsync();
		var ordered = results.Select(row => row.Item).ToList();

		await Assert.That(ordered[0]).IsSameReferenceAs(rated);
		await Assert.That(ordered[1]).IsSameReferenceAs(lowRated);
		await Assert.That(ordered[2]).IsSameReferenceAs(unrated);
	}

	[Test]
	public async Task Execute_WhenFilterRatingGreaterOrEqual_ExcludesLowerAndUnrated()
	{
		var high = MovieWithRating(2020, 9.0f);
		var low = MovieWithRating(2021, 6.0f);
		var unrated = MovieWithRating(2022, null);
		var options = new QueryOptions { Filter = Some.Of(Filter(Group(Number(nameof(MediaQueryRow.Rating), FilterPredicateOperator.GreaterOrEqual, 7.0)))) };

		var results = await _testee.ExecuteAsync([high, low, unrated], options, NoEnrollment).ToListAsync();

		await Assert.That(Items(results, QueryOutcome.Qualified)).IsEquivalentTo([high]);
		await Assert.That(Items(results, QueryOutcome.Culled)).IsEquivalentTo([low, unrated]);
	}

	[Test]
	public async Task Execute_WhenFilterRatingGreaterOrEqual_KeepsATitleThatReadsAsTheBound()
	{
		var exact = MovieWithRating(2020, 7.1f);
		var halfway = MovieWithRating(2021, 6.95f);
		var below = MovieWithRating(2022, 6.94f);
		var options = new QueryOptions { Filter = Some.Of(Filter(Group(Number(nameof(MediaQueryRow.Rating), FilterPredicateOperator.GreaterOrEqual, 7.0)))) };
		var strict = new QueryOptions { Filter = Some.Of(Filter(Group(Number(nameof(MediaQueryRow.Rating), FilterPredicateOperator.GreaterOrEqual, 7.1)))) };

		var results = await _testee.ExecuteAsync([exact, halfway, below], options, NoEnrollment).ToListAsync();
		var strictResults = await _testee.ExecuteAsync([exact, halfway, below], strict, NoEnrollment).ToListAsync();

		await Assert.That(Items(results, QueryOutcome.Qualified)).IsEquivalentTo([exact, halfway]);
		await Assert.That(Items(strictResults, QueryOutcome.Qualified)).IsEquivalentTo([exact]);
	}

	[Test]
	public async Task Execute_WhenFilterVotesGreaterOrEqual_ExcludesFewerAndThoseWithoutVotes()
	{
		var many = MovieWithRating(2020, 7.0f, votes: 900);
		var few = MovieWithRating(2021, 7.0f, votes: 300);
		var unvoted = MovieWithRating(2022, 7.0f, votes: null);
		var options = new QueryOptions { Filter = Some.Of(Filter(Group(Number(nameof(MediaQueryRow.Votes), FilterPredicateOperator.GreaterOrEqual, 500.0)))) };

		var results = await _testee.ExecuteAsync([many, few, unvoted], options, NoEnrollment).ToListAsync();

		await Assert.That(Items(results, QueryOutcome.Qualified)).IsEquivalentTo([many]);
		await Assert.That(Items(results, QueryOutcome.Culled)).IsEquivalentTo([few, unvoted]);
	}

	private static QueryPredicates<MediaQueryRow> NoEnrollment => new(IsWantlisted: _ => false, IsBlocklisted: _ => false, IsOwned: _ => false);

	private static IEnumerable<MediaQueryRow> Items(IEnumerable<QueryVerdict<MediaQueryRow>> rows, QueryOutcome outcome) =>
		rows.Where(row => row.Outcome == outcome).Select(row => row.Item);

	private static MediaQueryRow MovieWithRating(int year, float? rating, int? votes = 1)
	{
		return MediaQueryRow.From(
			new MatchedMedia(
				new MediaIdentity(MediaId.New(), [TmdbId.From(year.ToString(), MediaType.Movie)]),
				new MovieDetails
				{
					Title = $"Movie {year}",
					ReleaseDate = new DateOnly(year, 1, 1),
					Runtime = TimeSpan.FromMinutes(90),
					Rating = rating is { } value ? RatingSummary.From([new TmdbRating(value, Optional.Of(votes))]) : RatingSummary.None,
				}
			),
			new RatingStrategy(AveragedRating.Everything),
			[RatingSource.Tmdb]
		);
	}
}

internal record Item(string Name, int Year, string Genre = "Unknown");
