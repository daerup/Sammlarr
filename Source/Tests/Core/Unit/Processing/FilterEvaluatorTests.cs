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
		var result = FilterEvaluator.Build(Filter(Group(
			Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020),
			String("Genre", FilterPredicateOperator.Equals, "Action"))));

		await Assert.That(result).IsEqualTo("((Year >= 2020) and (Genre != null && Genre.ToLower() == \"Action\".ToLower()))");
	}

	[Test]
	public async Task Build_WhenMultipleOrPredicates_ReturnsOrExpression()
	{
		var result = FilterEvaluator.Build(Filter(FilterOperator.Or, Group(FilterOperator.Or,
			String("Genre", FilterPredicateOperator.Equals, "Action"),
			String("Genre", FilterPredicateOperator.Equals, "Comedy"))));

		await Assert
			.That(result)
			.IsEqualTo("((Genre != null && Genre.ToLower() == \"Action\".ToLower()) or (Genre != null && Genre.ToLower() == \"Comedy\".ToLower()))");
	}

	[Test]
	public async Task Build_WhenMultipleAndGroups_CombinesGroupsWithAnd()
	{
		var result = FilterEvaluator.Build(Filter(
			Group(Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020)),
			Group(String("Genre", FilterPredicateOperator.Equals, "Action"))));

		await Assert.That(result).IsEqualTo("((Year >= 2020) and (Genre != null && Genre.ToLower() == \"Action\".ToLower()))");
	}

	[Test]
	public async Task Build_WhenNestedGroups_WrapsNestedInParentheses()
	{
		var result = FilterEvaluator.Build(Filter(Group(FilterOperator.And,
			[Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020)],
			[Group(FilterOperator.Or,
				String("Genre", FilterPredicateOperator.Equals, "Action"),
				String("Genre", FilterPredicateOperator.Equals, "Comedy"))])));

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
}
