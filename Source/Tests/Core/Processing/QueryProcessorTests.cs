using System.ComponentModel;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Processing;
using Core.Processing;
using Test.Shared;
using TUnit.Assertions.Core;
using TUnit.Assertions.Enums;
using static Test.Shared.QueryFilterFactory;

namespace Core.Tests.Processing;

public class QueryProcessorTests
{
	private readonly QueryProcessor<Item> _testee = new();

	[Test]
	public async Task Execute_WhenNoOptionsProvided_ReturnsAllItems()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama") };

		var results = await _testee.Execute(items, new QueryOptions(), _ => false, _ => false).ToListAsync();
		await Assert.That(results).AreOfUnionType(typeof(Item), typeof(Item));
	}

	[Test]
	public async Task Execute_WhenLimitProvided_ReturnsWithSkippedUnion()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama") };
		var options = new QueryOptions { Limit = new QueryLimit { Count = 1 } };

		var results = await _testee.Execute(items, options, _ => false, _ => false).ToListAsync();

		await Assert.That(results).AreOfUnionType(typeof(Item), typeof(Truncated<Item>));
	}

	[Test]
	public async Task Execute_WhenSortProvided_ReturnsAllItemsAsValues()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama") };
		var options = new QueryOptions
		{
			Sort = new QuerySort { Property = nameof(Item.Year), Direction = ListSortDirection.Descending },
		};

		var results = await _testee.Execute(items, options, _ => false, _ => false).ToListAsync();
		await Assert.That(results).AreOfUnionType<IList<QueryResult<Item>>, QueryResult<Item>>(typeof(Item), typeof(Item));
		await Assert.That(results.OfUnionType<Item>()).IsEquivalentTo([items[1], items[0]], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_WhenSortProvided_ReturnsAllItemsAsValuesWithSortDirection()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama"), new Item("C", 2019, "Comedy") };
		var options = new QueryOptions
		{
			Limit = new QueryLimit { Count = 2 },
			Sort = new QuerySort { Property = nameof(Item.Year), Direction = ListSortDirection.Ascending },
		};

		var results = await _testee.Execute(items, options, _ => false, _ => false).ToListAsync();
		await Assert.That(results).AreOfUnionType(typeof(Item), typeof(Item), typeof(Truncated<Item>));
		await Assert.That(results.OfUnionType<Item>()).IsEquivalentTo([items[2], items[0]], CollectionOrdering.Matching);
		await Assert.That(results.OfUnionType<Truncated<Item>>().Select(i => i.Item)).IsEquivalentTo([items[1]]);
	}

	[Test]
	public async Task Execute_WhenFilterProvided_ReturnsOnlyMatchingFilter()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama"), new Item("C", 2019, "Comedy") };
		var options = new QueryOptions { Filter = Filter(Group(String("Genre", FilterPredicateOperator.Equals, "Drama"))) };

		var results = await _testee.Execute(items, options, _ => false, _ => false).ToListAsync();
		await Assert.That(results).AreOfUnionType(typeof(Culled<Item>), typeof(Item), typeof(Culled<Item>));
	}

	[Test]
	public async Task Execute_WhenFilterAndLimitProvided_ReturnsOnlyMatchingFilterAndLimit()
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
			Filter = Filter(
				FilterOperator.Or,
				Group(String("Genre", FilterPredicateOperator.Equals, "Drama")),
				Group(Number("Year", FilterPredicateOperator.LessThan, 2017))
			),
			Limit = new QueryLimit { Count = 2 },
		};

		var results = await _testee.Execute(items, options, _ => false, _ => false).ToListAsync();
		await Assert.That(results).AreOfUnionType(typeof(Culled<Item>), typeof(Item), typeof(Culled<Item>), typeof(Item), typeof(Truncated<Item>));
	}

	[Test]
	public async Task Execute_WhenOrFilterProvided_ReturnsOnlyMatchingOrFilter()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama"), new Item("C", 2019, "Comedy") };
		var options = new QueryOptions
		{
			Filter = Filter(
				FilterOperator.Or,
				Group(String("Genre", FilterPredicateOperator.Equals, "Drama")),
				Group(Number("Year", FilterPredicateOperator.LessOrEqual, 2019))
			),
		};
		var results = await _testee.Execute(items, options, _ => false, _ => false).ToListAsync();
		await Assert.That(results).AreOfUnionType(typeof(Culled<Item>), typeof(Item), typeof(Item));
	}

	[Test]
	public async Task Execute_WhenWatchlistedIsExemptAndItemIsWatchlisted_ItemIsIncludedAndDoesNotCountTowardLimit()
	{
		var watchlisted = new Item("Watchlisted", 2020, "Action");
		var normal = new Item("Normal", 2021, "Drama");
		var options = new QueryOptions
		{
			Limit = new QueryLimit { Count = 1, Watchlisted = SlotPolicy.Preserved },
		};

		var results = await _testee.Execute([watchlisted, normal], options, i => i == watchlisted, _ => false).ToListAsync();

		await Assert.That(results).AreOfUnionType(typeof(Bypassed<Item>), typeof(Item));
	}

	[Test]
	public async Task Execute_WhenBlacklistedIsExemptAndItemIsBlacklisted_ItemIsIncludedAndDoesNotCountTowardLimit()
	{
		var blacklisted = new Item("Blacklisted", 2020, "Action");
		var normal = new Item("Normal", 2021, "Drama");
		var options = new QueryOptions
		{
			Limit = new QueryLimit { Count = 1, Blacklisted = SlotPolicy.Preserved },
		};

		var results = await _testee.Execute([blacklisted, normal], options, _ => false, i => i == blacklisted).ToListAsync();

		await Assert.That(results).AreOfUnionType(typeof(Bypassed<Item>), typeof(Item));
		await Assert.That(results.OfUnionType<Bypassed<Item>>().Single().Source).IsEqualTo(BypassReason.Blacklisted);
	}

	[Test]
	public async Task Execute_WhenExemptItemsAreMixedWithNonExempt_OnlyNonExemptCountTowardLimit()
	{
		var watchlisted = new Item("Watchlisted", 2019, "Action");
		var normal1 = new Item("Normal1", 2020, "Drama");
		var normal2 = new Item("Normal2", 2021, "Comedy");
		var options = new QueryOptions
		{
			Limit = new QueryLimit { Count = 1, Watchlisted = SlotPolicy.Preserved },
		};

		var results = await _testee.Execute([watchlisted, normal1, normal2], options, i => i == watchlisted, _ => false).ToListAsync();

		await Assert.That(results).AreOfUnionType(typeof(Bypassed<Item>), typeof(Item), typeof(Truncated<Item>));
	}

	[Test]
	public async Task Execute_WhenLimitIsReached_ExemptItemsAreStillIncluded()
	{
		var normal1 = new Item("Normal1", 2019, "Action");
		var watchlisted = new Item("Watchlisted", 2020, "Drama");
		var normal2 = new Item("Normal2", 2021, "Comedy");
		var options = new QueryOptions
		{
			Limit = new QueryLimit { Count = 1, Watchlisted = SlotPolicy.Preserved },
		};

		var results = await _testee.Execute([normal1, watchlisted, normal2], options, i => i == watchlisted, _ => false).ToListAsync();

		await Assert.That(results).AreOfUnionType(typeof(Item), typeof(Truncated<Item>), typeof(Truncated<Item>));
	}

	[Test]
	public async Task Execute_WhenWatchlistedIsIncluded_MatchedItemEmitsExemptWithCountsTowardLimitTrue()
	{
		var watchlisted = new Item("Watchlisted", 2020, "Action");
		var normal = new Item("Normal", 2021, "Drama");
		var options = new QueryOptions { Limit = new QueryLimit { Count = 2 } };

		var results = await _testee.Execute([watchlisted, normal], options, i => i == watchlisted, _ => false).ToListAsync();

		await Assert.That(results).AreOfUnionType(typeof(Bypassed<Item>), typeof(Item));
		await Assert.That(results.OfUnionType<Bypassed<Item>>().Single().Source).IsEqualTo(BypassReason.Watchlisted);
		await Assert.That(results.OfUnionType<Bypassed<Item>>().Single().CountsTowardLimit).IsTrue();
	}

	[Test]
	public async Task Execute_WhenItemIsWatchlistedAndBlacklisted_WatchlistTakesPrecedenceOverSlotPolicy()
	{
		var both = new Item("Both", 2020, "Action");
		var normal = new Item("Normal", 2021, "Drama");
		var options = new QueryOptions
		{
			Limit = new QueryLimit
			{
				Count = 1,
				Watchlisted = SlotPolicy.Preserved,
				Blacklisted = SlotPolicy.Consumed,
			},
		};

		var results = await _testee.Execute([both, normal], options, i => i == both, i => i == both).ToListAsync();

		await Assert.That(results).AreOfUnionType(typeof(Bypassed<Item>), typeof(Item));
		var bypassed = results.OfUnionType<Bypassed<Item>>().Single();
		await Assert.That(bypassed.Source).IsEqualTo(BypassReason.Watchlisted);
		await Assert.That(bypassed.CountsTowardLimit).IsFalse();
	}

	[Test]
	public async Task Execute_WhenItemIsWatchlistedAndBlacklisted_WatchlistTakesPrecedenceOverSlotPolicy2()
	{
		var both = new Item("Both", 2020, "Action");
		var normal = new Item("Normal", 2021, "Drama");
		var options = new QueryOptions
		{
			Limit = new QueryLimit
			{
				Count = 1,
				Watchlisted = SlotPolicy.Consumed,
				Blacklisted = SlotPolicy.Preserved,
			},
		};

		var results = await _testee.Execute([both, normal], options, i => i == both, i => i == both).ToListAsync();

		await Assert.That(results).AreOfUnionType(typeof(Bypassed<Item>), typeof(Truncated<Item>));
		var bypassed = results.OfUnionType<Bypassed<Item>>().Single();
		await Assert.That(bypassed.Source).IsEqualTo(BypassReason.Watchlisted);
		await Assert.That(bypassed.CountsTowardLimit).IsTrue();
	}

	[Test]
	public async Task Execute_WhenWatchlistedIsIncludedAndConsumesOnlySlot_NextItemIsSkipped()
	{
		var watchlisted = new Item("Watchlisted", 2020, "Action");
		var normal = new Item("Normal", 2021, "Drama");
		var options = new QueryOptions { Limit = new QueryLimit { Count = 1 } };

		var results = await _testee.Execute([watchlisted, normal], options, i => i == watchlisted, _ => false).ToListAsync();

		await Assert.That(results).AreOfUnionType(typeof(Bypassed<Item>), typeof(Truncated<Item>));
	}
}

public class QueryProcessorRatingTests
{
	private readonly QueryProcessor<IMediaDetail> _testee = new();

	[Test]
	public async Task Execute_WhenSortedByRatingDescending_ItemsWithoutRatingComeLast()
	{
		var rated = MovieWithRating(2020, 8.5f);
		var lowRated = MovieWithRating(2021, 5.0f);
		var unrated = MovieWithRating(2022, null);
		var options = new QueryOptions
		{
			Sort = new QuerySort { Property = nameof(IMediaDetail.Rating), Direction = ListSortDirection.Descending },
		};

		var results = await _testee.Execute([lowRated, unrated, rated], options, _ => false, _ => false).ToListAsync();
		var ordered = results.OfUnionType<IMediaDetail>().ToList();

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
		var options = new QueryOptions { Filter = Filter(Group(Number(nameof(IMediaDetail.Rating), FilterPredicateOperator.GreaterOrEqual, 7.0))) };

		var results = await _testee.Execute([high, low, unrated], options, _ => false, _ => false).ToListAsync();

		await Assert.That(results.OfUnionType<IMediaDetail>()).IsEquivalentTo([(IMediaDetail)high]);
		await Assert.That(results.OfUnionType<Culled<IMediaDetail>>().Select(c => c.Item)).IsEquivalentTo(new IMediaDetail[] { low, unrated });
	}

	private static MovieDetail MovieWithRating(int year, float? rating) =>
		new()
		{
			Identity = new MediaIdentity(default, [TmdbId.From(year.ToString(), MediaType.Movie)]),
			Title = $"Movie {year}",
			Tagline = string.Empty,
			Summary = string.Empty,
			PosterUrl = string.Empty,
			BackdropUrl = string.Empty,
			ReleaseDate = new DateOnly(year, 1, 1),
			Genres = [],
			Runtime = TimeSpan.FromMinutes(90),
			Ratings = rating is { } value ? [new MediaRating(RatingSource.Tmdb, value, 1)] : [],
		};
}

internal record Item(string Name, int Year, string Genre = "Unknown");

file static class Extension
{
	public static IEnumerable<TOut> OfUnionType<TOut>(this IEnumerable<QueryResult<Item>> source) =>
		EnumerableExtensions.OfUnionType<QueryResult<Item>, TOut>(source);

	public static IEnumerable<TOut> OfUnionType<TOut>(this IEnumerable<QueryResult<IMediaDetail>> source) =>
		EnumerableExtensions.OfUnionType<QueryResult<IMediaDetail>, TOut>(source);

	public static UnionCollectionAssertion<TCollection, QueryResult<Item>> AreOfUnionType<TCollection>(
		this IAssertionSource<TCollection> source,
		params IEnumerable<Type> expectedTypes
	)
		where TCollection : IEnumerable<QueryResult<Item>>
	{
		using var _ = Assert.Multiple();
		return new UnionCollectionAssertion<TCollection, QueryResult<Item>>(source.Context, expectedTypes);
	}
}
