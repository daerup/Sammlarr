using Contracts.Processing;
using Core.Processing;
using Test.Shared;
using static Test.Shared.QueryFilterFactory;

namespace Core.Tests.Unit.Processing;

public class FilterEvaluatorTests
{
	[Test]
	public async Task Build_WhenNoGroups_ReturnsEmpty()
	{
		var result = FilterEvaluator.Build(new QueryFilter { Operator = FilterOperator.And, Groups = [] });
		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task Build_WhenEmptyGroup_ReturnsEmpty()
	{
		var result = FilterEvaluator.Build(Filter(Group()));
		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task Build_WhenSingleNumberComparison_ReturnsPredicate()
	{
		var result = FilterEvaluator.Build(Filter(Group(Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020))));
		await Assert.That(result).IsEqualTo("(Year >= 2020)");
	}

	[Test]
	public async Task Build_WhenMultipleAndPredicates_ReturnsAndExpression()
	{
		var result = FilterEvaluator.Build(
			Filter(Group(Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020), String("Genre", FilterPredicateOperator.Equals, "Action")))
		);

		await Assert.That(result).IsEqualTo("((Year >= 2020) and (Genre != null && Genre.ToLower() == \"Action\".ToLower()))");
	}

	[Test]
	public async Task Build_WhenMultipleOrPredicates_ReturnsOrExpression()
	{
		var result = FilterEvaluator.Build(
			Filter(
				FilterOperator.Or,
				Group(FilterOperator.Or, String("Genre", FilterPredicateOperator.Equals, "Action"), String("Genre", FilterPredicateOperator.Equals, "Comedy"))
			)
		);

		await Assert
			.That(result)
			.IsEqualTo("((Genre != null && Genre.ToLower() == \"Action\".ToLower()) or (Genre != null && Genre.ToLower() == \"Comedy\".ToLower()))");
	}

	[Test]
	public async Task Build_WhenMultipleAndGroups_CombinesGroupsWithAnd()
	{
		var result = FilterEvaluator.Build(
			Filter(Group(Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020)), Group(String("Genre", FilterPredicateOperator.Equals, "Action")))
		);

		await Assert.That(result).IsEqualTo("((Year >= 2020) and (Genre != null && Genre.ToLower() == \"Action\".ToLower()))");
	}

	[Test]
	public async Task Build_WhenNestedGroups_WrapsNestedInParentheses()
	{
		var result = FilterEvaluator.Build(
			Filter(
				Group(
					FilterOperator.And,
					[Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020)],
					[
						Group(
							FilterOperator.Or,
							String("Genre", FilterPredicateOperator.Equals, "Action"),
							String("Genre", FilterPredicateOperator.Equals, "Comedy")
						),
					]
				)
			)
		);

		await Assert
			.That(result)
			.IsEqualTo(
				"((Year >= 2020) and ((Genre != null && Genre.ToLower() == \"Action\".ToLower()) or (Genre != null && Genre.ToLower() == \"Comedy\".ToLower())))"
			);
	}

	[Test]
	public async Task Build_WhenGroupContainsOnlyEmptyNestedGroups_ReturnsEmpty()
	{
		var result = FilterEvaluator.Build(Filter(Group(FilterOperator.And, [], [Group()])));
		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task Build_WhenStringContains_LowercasesBothSides()
	{
		var result = FilterEvaluator.Build(Filter(Group(String("Title", FilterPredicateOperator.Contains, "Foo"))));

		await Assert.That(result).IsEqualTo("(Title != null && Title.ToLower().Contains(\"Foo\".ToLower()))");
	}

	[Test]
	public async Task Build_WhenStringStartsWith_LowercasesBothSides()
	{
		var result = FilterEvaluator.Build(Filter(Group(String("Title", FilterPredicateOperator.StartsWith, "Foo"))));

		await Assert.That(result).IsEqualTo("(Title != null && Title.ToLower().StartsWith(\"Foo\".ToLower()))");
	}

	[Test]
	public async Task Build_WhenStringEndsWith_LowercasesBothSides()
	{
		var result = FilterEvaluator.Build(Filter(Group(String("Title", FilterPredicateOperator.EndsWith, "Foo"))));

		await Assert.That(result).IsEqualTo("(Title != null && Title.ToLower().EndsWith(\"Foo\".ToLower()))");
	}

	[Test]
	public async Task Build_WhenStringNotContains_LowercasesBothSides()
	{
		var result = FilterEvaluator.Build(Filter(Group(String("Title", FilterPredicateOperator.NotContains, "Foo"))));

		await Assert.That(result).IsEqualTo("(Title == null || !Title.ToLower().Contains(\"Foo\".ToLower()))");
	}

	[Test]
	public async Task Build_WhenStringIsEmpty_ChecksNullOrEmptyString()
	{
		var result = FilterEvaluator.Build(
			Filter(
				Group(
					new FilterPredicate
					{
						Field = "Title",
						Operator = FilterPredicateOperator.IsEmpty,
						ValueKind = FilterValueKind.String,
					}
				)
			)
		);

		await Assert.That(result).IsEqualTo("(Title == null || Title == \"\")");
	}

	[Test]
	public async Task Build_WhenNumberIsEmpty_ChecksNullOnly()
	{
		var result = FilterEvaluator.Build(
			Filter(
				Group(
					new FilterPredicate
					{
						Field = "Rating",
						Operator = FilterPredicateOperator.IsEmpty,
						ValueKind = FilterValueKind.Number,
					}
				)
			)
		);

		await Assert.That(result).IsEqualTo("(Rating == null)");
	}

	[Test]
	public async Task Build_WhenGenresContains_UsesAnyOnCollection()
	{
		var result = FilterEvaluator.Build(Filter(Group(String("Genres", FilterPredicateOperator.Contains, "action"))));

		await Assert.That(result).IsEqualTo("(Genres.Any(g => g.ToLower().Contains(\"action\".ToLower())))");
	}

	[Test]
	public async Task Build_WhenGenresEquals_UsesAnyEqualityOnCollection()
	{
		var result = FilterEvaluator.Build(Filter(Group(String("Genres", FilterPredicateOperator.Equals, "Action"))));

		await Assert.That(result).IsEqualTo("(Genres.Any(g => g.ToLower() == \"Action\".ToLower()))");
	}

	[Test]
	public async Task Build_WhenGenresIsEmpty_ChecksNullOrCount()
	{
		var result = FilterEvaluator.Build(
			Filter(
				Group(
					new FilterPredicate
					{
						Field = "Genres",
						Operator = FilterPredicateOperator.IsEmpty,
						ValueKind = FilterValueKind.String,
					}
				)
			)
		);

		await Assert.That(result).IsEqualTo("(Genres == null || Genres.Count == 0)");
	}

	[Test]
	public async Task Build_WhenIn_EmitsDynamicLinqInExpression()
	{
		var result = FilterEvaluator.Build(Filter(Group(In("MediaType", "Movie", "Show"))));

		await Assert.That(result).IsEqualTo("(MediaType.ToString() in (\"Movie\", \"Show\"))");
	}

	[Test]
	public async Task Build_WhenNotIn_NegatesInExpression()
	{
		var result = FilterEvaluator.Build(Filter(Group(NotIn("MediaType", "Movie"))));

		await Assert.That(result).IsEqualTo("(!(MediaType.ToString() in (\"Movie\")))");
	}

	[Test]
	public async Task Build_WhenInWithEmptyList_ReturnsEmpty()
	{
		var result = FilterEvaluator.Build(Filter(Group(In("MediaType"))));

		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task Build_WhenDateIsNotEmpty_ChecksNotNullOnly()
	{
		var result = FilterEvaluator.Build(
			Filter(
				Group(
					new FilterPredicate
					{
						Field = "ReleaseDate",
						Operator = FilterPredicateOperator.IsNotEmpty,
						ValueKind = FilterValueKind.Date,
					}
				)
			)
		);

		await Assert.That(result).IsEqualTo("(ReleaseDate != null)");
	}
}
