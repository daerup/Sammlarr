using System.Text.Json;
using Core.Processing;
using Domain.Media;
using Domain.Processing;
using TUnit.Assertions.Enums;
using static TestInfrastructure.QueryFilterFactory;

namespace Core.Tests.Processing;

public class FilterEvaluatorTests
{
	private static readonly QueryPredicates<FilterRow> NoEnrollment = new(
		IsWantlisted: _ => false,
		IsBlocklisted: _ => false,
		IsOwned: _ => false,
		Identity: _ => Guid.Empty
	);

	[Test]
	public async Task Execute_ForEmptyNestedGroupsBesideAPredicate_AppliesOnlyThePredicate()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(FilterOperator.And, [Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020)], [Group(), Group(FilterOperator.Or, [], [Group()])])),
			new FilterRow { Year = 2019 },
			new FilterRow { Year = 2020 }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForAnInWithAnEmptyListBesideAPredicate_AppliesOnlyThePredicate()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(In("MediaType"), Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020))),
			new FilterRow { Year = 2019, MediaType = MediaType.Movie },
			new FilterRow { Year = 2020, MediaType = MediaType.Show }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForANumberComparison_QualifiesTheRowsItHoldsFor()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020))),
			new FilterRow { Year = 2019 },
			new FilterRow { Year = 2020 },
			new FilterRow { Year = 2021 }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForAWholeNumberAgainstADecimalField_ComparesTheirValues()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(Number("Rating", FilterPredicateOperator.GreaterOrEqual, 7))),
			new FilterRow { Rating = 6.9 },
			new FilterRow { Rating = 7.5 },
			new FilterRow()
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForAFractionAgainstAnOptionalWholeNumber_ComparesTheirValues()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(Number("Votes", FilterPredicateOperator.GreaterThan, 7.5))),
			new FilterRow { Votes = 7 },
			new FilterRow { Votes = 8 },
			new FilterRow()
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForAnInWithNumbersOnAnOptionalWholeNumber_QualifiesTheListedValues()
	{
		var predicate = new FilterPredicate
		{
			Field = "Votes",
			Operator = FilterPredicateOperator.In,
			ValueKind = FilterValueKind.Number,
			Value = "[500, 1000]",
		};

		var outcomes = await OutcomesAsync(Filter(Group(predicate)), new FilterRow { Votes = 500 }, new FilterRow { Votes = 501 }, new FilterRow());

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Culled, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForADateComparison_ComparesByDayAndCullsRowsWithoutADate()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(Date("ReleaseDate", FilterPredicateOperator.GreaterThan, new DateOnly(2020, 1, 1)))),
			new FilterRow(),
			new FilterRow { ReleaseDate = new DateOnly(2020, 1, 1) },
			new FilterRow { ReleaseDate = new DateOnly(2020, 1, 2) }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Culled, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForADatePredicateWithoutAValue_ComparesWithDayZero()
	{
		var predicate = new FilterPredicate
		{
			Field = "ReleaseDate",
			Operator = FilterPredicateOperator.GreaterThan,
			ValueKind = FilterValueKind.Date,
		};

		var outcomes = await OutcomesAsync(
			Filter(Group(predicate)),
			new FilterRow { ReleaseDate = DateOnly.MinValue },
			new FilterRow { ReleaseDate = DateOnly.MinValue.AddDays(1) }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForIsNotEmptyOnText_CullsMissingAndEmptyText()
	{
		var predicate = new FilterPredicate
		{
			Field = "Tagline",
			Operator = FilterPredicateOperator.IsNotEmpty,
			ValueKind = FilterValueKind.String,
		};

		var outcomes = await OutcomesAsync(Filter(Group(predicate)), new FilterRow(), new FilterRow { Tagline = "" }, new FilterRow { Tagline = "A dream." });

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Culled, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForTwoPredicatesJoinedByAnd_QualifiesRowsMatchingBothRegardlessOfCase()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020), String("Genre", FilterPredicateOperator.Equals, "Action"))),
			new FilterRow { Year = 2020, Genre = "Action" },
			new FilterRow { Year = 2021, Genre = "action" },
			new FilterRow { Year = 2019, Genre = "Action" },
			new FilterRow { Year = 2020, Genre = "Drama" },
			new FilterRow { Year = 2020 }
		);

		await Assert
			.That(outcomes)
			.IsEquivalentTo(
				[QueryOutcome.Qualified, QueryOutcome.Qualified, QueryOutcome.Culled, QueryOutcome.Culled, QueryOutcome.Culled],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Execute_ForTwoPredicatesJoinedByOr_QualifiesRowsMatchingEither()
	{
		var outcomes = await OutcomesAsync(
			Filter(
				FilterOperator.Or,
				Group(FilterOperator.Or, String("Genre", FilterPredicateOperator.Equals, "Action"), String("Genre", FilterPredicateOperator.Equals, "Comedy"))
			),
			new FilterRow { Genre = "Action" },
			new FilterRow { Genre = "Comedy" },
			new FilterRow { Genre = "Drama" }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Qualified, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForTwoGroupsJoinedByAnd_QualifiesRowsMatchingBoth()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020)), Group(String("Genre", FilterPredicateOperator.Equals, "Action"))),
			new FilterRow { Year = 2020, Genre = "Action" },
			new FilterRow { Year = 2019, Genre = "Action" },
			new FilterRow { Year = 2020, Genre = "Drama" }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Culled, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForANestedOrGroup_AppliesItAsOneTermOfTheOuterAnd()
	{
		var outcomes = await OutcomesAsync(
			Filter(
				Group(
					FilterOperator.And,
					[Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020)],
					[Group(FilterOperator.Or, String("Genre", FilterPredicateOperator.Equals, "Action"), String("Genre", FilterPredicateOperator.Equals, "Comedy"))]
				)
			),
			new FilterRow { Year = 2020, Genre = "Action" },
			new FilterRow { Year = 2020, Genre = "Comedy" },
			new FilterRow { Year = 2020, Genre = "Drama" },
			new FilterRow { Year = 2019, Genre = "Action" }
		);

		await Assert
			.That(outcomes)
			.IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Qualified, QueryOutcome.Culled, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForAContainsOnText_IgnoresCaseAndCullsMissingText()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(String("Title", FilterPredicateOperator.Contains, "Foo"))),
			new FilterRow { Title = "The FOOL" },
			new FilterRow { Title = "Bar" },
			new FilterRow()
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Culled, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForANumberThatReadsLikeAnExpression_RefusesIt()
	{
		var predicate = new FilterPredicate
		{
			Field = "Year",
			Operator = FilterPredicateOperator.GreaterOrEqual,
			ValueKind = FilterValueKind.Number,
			Value = "2030 || true",
		};

		await Assert.That(async () => await OutcomesAsync(Filter(Group(predicate)), new FilterRow { Year = 2020 })).Throws<JsonException>();
	}

	[Test]
	public async Task Execute_ForTheSameFilterWithAnotherValue_AppliesTheNewValue()
	{
		var testee = new QueryProcessor<FilterRow>();
		FilterRow[] rows = [new() { Year = 2019 }, new() { Year = 2021 }];

		await testee
			.ExecuteAsync(rows, new QueryOptions { Filter = Some.Of(Filter(Group(Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020)))) }, NoEnrollment)
			.ToListAsync();
		var verdicts = await testee
			.ExecuteAsync(rows, new QueryOptions { Filter = Some.Of(Filter(Group(Number("Year", FilterPredicateOperator.GreaterOrEqual, 2010)))) }, NoEnrollment)
			.ToListAsync();

		await Assert
			.That(verdicts.Select(verdict => verdict.Outcome))
			.IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForAStartsWithOnText_IgnoresCaseAndCullsMissingText()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(String("Title", FilterPredicateOperator.StartsWith, "Foo"))),
			new FilterRow { Title = "foobar" },
			new FilterRow { Title = "Barfoo" },
			new FilterRow()
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Culled, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForAnEndsWithOnText_IgnoresCaseAndCullsMissingText()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(String("Title", FilterPredicateOperator.EndsWith, "Foo"))),
			new FilterRow { Title = "BarFOO" },
			new FilterRow { Title = "Foobar" },
			new FilterRow()
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Culled, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForANotContainsOnText_IgnoresCaseAndQualifiesMissingText()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(String("Title", FilterPredicateOperator.NotContains, "Foo"))),
			new FilterRow { Title = "FOO Fighters" },
			new FilterRow { Title = "Bar" },
			new FilterRow()
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForIsEmptyOnText_QualifiesMissingAndEmptyText()
	{
		var predicate = new FilterPredicate
		{
			Field = "Title",
			Operator = FilterPredicateOperator.IsEmpty,
			ValueKind = FilterValueKind.String,
		};

		var outcomes = await OutcomesAsync(Filter(Group(predicate)), new FilterRow(), new FilterRow { Title = "" }, new FilterRow { Title = "Heat" });

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Qualified, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForIsEmptyOnANumber_QualifiesOnlyAMissingNumber()
	{
		var predicate = new FilterPredicate
		{
			Field = "Rating",
			Operator = FilterPredicateOperator.IsEmpty,
			ValueKind = FilterValueKind.Number,
		};

		var outcomes = await OutcomesAsync(Filter(Group(predicate)), new FilterRow(), new FilterRow { Rating = 0 }, new FilterRow { Rating = 7.5 });

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Culled, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForAContainsOnGenres_QualifiesRowsWithAnyGenreContainingIt()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(String("Genres", FilterPredicateOperator.Contains, "action"))),
			new FilterRow { Genres = ["Drama", "Action Comedy"] },
			new FilterRow { Genres = ["Drama"] },
			new FilterRow { Genres = [] }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Culled, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForAnEqualsOnGenres_QualifiesRowsWithAnyGenreEqualToItRegardlessOfCase()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(String("Genres", FilterPredicateOperator.Equals, "Action"))),
			new FilterRow { Genres = ["Drama", "ACTION"] },
			new FilterRow { Genres = ["Action Comedy"] }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForIsEmptyOnGenres_QualifiesMissingAndEmptyGenres()
	{
		var predicate = new FilterPredicate
		{
			Field = "Genres",
			Operator = FilterPredicateOperator.IsEmpty,
			ValueKind = FilterValueKind.String,
		};

		var outcomes = await OutcomesAsync(Filter(Group(predicate)), new FilterRow(), new FilterRow { Genres = [] }, new FilterRow { Genres = ["Drama"] });

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Qualified, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForAnInOnTheMediaType_QualifiesTheListedTypes()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(In("MediaType", "Movie", "Show"))),
			new FilterRow { MediaType = MediaType.Movie },
			new FilterRow { MediaType = MediaType.Show },
			new FilterRow { MediaType = MediaType.Unknown }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Qualified, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForANotInOnTheMediaType_CullsTheListedTypes()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(NotIn("MediaType", "Movie"))),
			new FilterRow { MediaType = MediaType.Movie },
			new FilterRow { MediaType = MediaType.Show }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForIsNotEmptyOnADate_CullsOnlyAMissingDate()
	{
		var predicate = new FilterPredicate
		{
			Field = "ReleaseDate",
			Operator = FilterPredicateOperator.IsNotEmpty,
			ValueKind = FilterValueKind.Date,
		};

		var outcomes = await OutcomesAsync(Filter(Group(predicate)), new FilterRow(), new FilterRow { ReleaseDate = DateOnly.MinValue });

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForGenresContainingAnyOf_MatchesPickedGenresExactlyAndTextAnywhere()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(Genres(FilterPredicateOperator.In, new GenreMatch(["Sci-Fi & Fantasy"], ["anim"])))),
			new FilterRow { Genres = ["Drama"] },
			new FilterRow { Genres = ["Sci-Fi & Fantasy"] },
			new FilterRow { Genres = ["Science Fiction"] },
			new FilterRow { Genres = ["Anime"] }
		);

		await Assert
			.That(outcomes)
			.IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified, QueryOutcome.Culled, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForGenresContainingNoneOf_CullsEveryTitleCarryingOne()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(Genres(FilterPredicateOperator.NotIn, new GenreMatch(["Animation"], ["anime"])))),
			new FilterRow { Genres = ["Drama", "Animation"] },
			new FilterRow { Genres = ["Anime"] },
			new FilterRow { Genres = ["Documentary"] }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Culled, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForGenresContainingAll_KeepsOnlyTitlesCarryingEveryPickAndText()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(Genres(FilterPredicateOperator.ContainsAll, new GenreMatch(["Drama", "Comedy"], ["anim"])))),
			new FilterRow { Genres = ["Drama", "Comedy", "Animation"] },
			new FilterRow { Genres = ["Drama", "Animation"] },
			new FilterRow { Genres = ["Drama", "Comedy"] }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Culled, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForTypedGenreTextWithQuotesAndBackslashes_MatchesItLiterally()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(Genres(FilterPredicateOperator.In, new GenreMatch([], ["a\"b\\", "\") || true || (\""])))),
			new FilterRow { Genres = ["xa\"b\\x"] },
			new FilterRow { Genres = ["zzz"] }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForAnEmptyTopLevelGroupBesideAnother_AppliesOnlyTheOther()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(), Group(Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020))),
			new FilterRow { Year = 2019 },
			new FilterRow { Year = 2020 }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	[Arguments(FilterPredicateOperator.In)]
	[Arguments(FilterPredicateOperator.ContainsAll)]
	public async Task Execute_ForGenresWithNothingPicked_KeepsEveryTitle(FilterPredicateOperator op)
	{
		var outcomes = await OutcomesAsync(Filter(Group(Genres(op, GenreMatch.None))), new FilterRow { Genres = ["Drama"] });

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Qualified]);
	}

	[Test]
	[Arguments(FilterPredicateOperator.Equals)]
	[Arguments(FilterPredicateOperator.NotEquals)]
	[Arguments(FilterPredicateOperator.LessOrEqual)]
	public async Task Execute_ForASeasonsConditionOnAMovie_CullsItWhateverTheOperator(FilterPredicateOperator op)
	{
		var outcomes = await OutcomesAsync(Filter(Group(Number("Seasons", op, 3))), new FilterRow());

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled]);
	}

	[Test]
	public async Task Execute_ForAnEpisodesConditionOnShows_JudgesTheirCount()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(Number("Episodes", FilterPredicateOperator.NotEquals, 10))),
			new FilterRow { Episodes = 10 },
			new FilterRow { Episodes = 12 }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForARelativeDate_ComparesWithTheDayItNamesFromToday()
	{
		var outcomes = await OutcomesAsync(
			Filter(Group(Date("ReleaseDate", FilterPredicateOperator.GreaterThan, new RelativeDate(2, DateUnit.Years, Ahead: false)))),
			new FilterRow { ReleaseDate = new DateOnly(2024, 10, 18) },
			new FilterRow { ReleaseDate = new DateOnly(2024, 10, 19) }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_ForABetweenWithAFixedAndARelativeEnd_KeepsTheDaysBetweenThem()
	{
		var between = Date("ReleaseDate", FilterPredicateOperator.Between, new DateOnly(1980, 1, 1)) with
		{
			ValueEnd = JsonSerializer.Serialize(new RelativeDate(10, DateUnit.Years, Ahead: false).ToIso()),
		};

		var outcomes = await OutcomesAsync(
			Filter(Group(between)),
			new FilterRow { ReleaseDate = new DateOnly(1979, 12, 31) },
			new FilterRow { ReleaseDate = new DateOnly(2016, 10, 18) },
			new FilterRow { ReleaseDate = new DateOnly(2016, 10, 19) }
		);

		await Assert.That(outcomes).IsEquivalentTo([QueryOutcome.Culled, QueryOutcome.Qualified, QueryOutcome.Culled], CollectionOrdering.Matching);
	}

	private static FilterPredicate Genres(FilterPredicateOperator op, GenreMatch match)
	{
		return new FilterPredicate
		{
			Field = "Genres",
			Operator = op,
			Value = JsonSerializer.Serialize(match),
		};
	}

	private static async Task<IReadOnlyList<QueryOutcome>> OutcomesAsync(QueryFilter filter, params FilterRow[] rows)
	{
		var options = new QueryOptions { Filter = Some.Of(filter), Today = new DateOnly(2026, 10, 18) };
		var verdicts = await new QueryProcessor<FilterRow>().ExecuteAsync(rows, options, NoEnrollment).ToListAsync();
		return [.. verdicts.Select(verdict => verdict.Outcome)];
	}

	private sealed record FilterRow
	{
		public int Year { get; init; }

		public DateOnly? ReleaseDate { get; init; }

		public string? Title { get; init; }

		public string? Tagline { get; init; }

		public string? Genre { get; init; }

		public double? Rating { get; init; }

		public int? Votes { get; init; }

		public List<string>? Genres { get; init; }

		public MediaType MediaType { get; init; }

		public int? Seasons { get; init; }

		public int? Episodes { get; init; }
	}
}
