using System.Text.Json;
using Contracts.Processing;
using Core.Processing;

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
		var filter = new QueryFilter { Operator = FilterOperator.And, Groups = [new FilterGroup { Operator = FilterOperator.And, Predicates = [] }] };

		var result = FilterEvaluator.Build(filter);

		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task Build_WhenSingleNumberComparison_ReturnsPredicate()
	{
		var filter = new QueryFilter
		{
			Operator = FilterOperator.And,
			Groups = [new FilterGroup { Operator = FilterOperator.And, Predicates = [Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020)] }],
		};

		var result = FilterEvaluator.Build(filter);

		await Assert.That(result).IsEqualTo("(Year >= 2020)");
	}

	[Test]
	public async Task Build_WhenMultipleAndPredicates_ReturnsAndExpression()
	{
		var filter = new QueryFilter
		{
			Operator = FilterOperator.And,
			Groups =
			[
				new FilterGroup
				{
					Operator = FilterOperator.And,
					Predicates = [Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020), StringEquals("Genre", "Action")],
				},
			],
		};

		var result = FilterEvaluator.Build(filter);

		await Assert.That(result).IsEqualTo("((Year >= 2020) and (Genre != null && Genre.ToLower() == \"Action\".ToLower()))");
	}

	[Test]
	public async Task Build_WhenMultipleOrPredicates_ReturnsOrExpression()
	{
		var filter = new QueryFilter
		{
			Operator = FilterOperator.Or,
			Groups = [new FilterGroup { Operator = FilterOperator.Or, Predicates = [StringEquals("Genre", "Action"), StringEquals("Genre", "Comedy")] }],
		};

		var result = FilterEvaluator.Build(filter);

		await Assert
			.That(result)
			.IsEqualTo("((Genre != null && Genre.ToLower() == \"Action\".ToLower()) or (Genre != null && Genre.ToLower() == \"Comedy\".ToLower()))");
	}

	[Test]
	public async Task Build_WhenMultipleAndGroups_CombinesGroupsWithAnd()
	{
		var filter = new QueryFilter
		{
			Operator = FilterOperator.And,
			Groups =
			[
				new FilterGroup { Operator = FilterOperator.And, Predicates = [Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020)] },
				new FilterGroup { Operator = FilterOperator.And, Predicates = [StringEquals("Genre", "Action")] },
			],
		};

		var result = FilterEvaluator.Build(filter);

		await Assert.That(result).IsEqualTo("((Year >= 2020) and (Genre != null && Genre.ToLower() == \"Action\".ToLower()))");
	}

	[Test]
	public async Task Build_WhenNestedGroups_WrapsNestedInParentheses()
	{
		var filter = new QueryFilter
		{
			Operator = FilterOperator.And,
			Groups =
			[
				new FilterGroup
				{
					Operator = FilterOperator.And,
					Predicates = [Number("Year", FilterPredicateOperator.GreaterOrEqual, 2020)],
					Groups =
					[
						new FilterGroup { Operator = FilterOperator.Or, Predicates = [StringEquals("Genre", "Action"), StringEquals("Genre", "Comedy")] },
					],
				},
			],
		};

		var result = FilterEvaluator.Build(filter);

		await Assert
			.That(result)
			.IsEqualTo(
				"((Year >= 2020) and ((Genre != null && Genre.ToLower() == \"Action\".ToLower()) or (Genre != null && Genre.ToLower() == \"Comedy\".ToLower())))"
			);
	}

	[Test]
	public async Task Build_WhenGroupContainsOnlyEmptyNestedGroups_ReturnsEmpty()
	{
		var filter = new QueryFilter
		{
			Operator = FilterOperator.And,
			Groups =
			[
				new FilterGroup
				{
					Operator = FilterOperator.And,
					Predicates = [],
					Groups = [new FilterGroup { Operator = FilterOperator.And, Predicates = [] }],
				},
			],
		};

		var result = FilterEvaluator.Build(filter);
		await Assert.That(result).IsEmpty();
	}

	private static FilterPredicate Number(string field, FilterPredicateOperator op, double value) =>
		new()
		{
			Field = field,
			Operator = op,
			ValueKind = FilterValueKind.Number,
			Value = JsonSerializer.Serialize(value),
		};

	private static FilterPredicate StringEquals(string field, string value) =>
		new()
		{
			Field = field,
			Operator = FilterPredicateOperator.Equals,
			ValueKind = FilterValueKind.String,
			Value = JsonSerializer.Serialize(value),
		};
}
