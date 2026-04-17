using Contracts.Processing;
using Core.Processing;

namespace Core.Tests.Unit.Processing;

public class FilterEvaluatorTests
{
	[Test]
	public async Task Build_WhenNoGroups_ReturnsNull()
	{
		var result = FilterEvaluator.Build(new QueryFilter { Operator = FilterOperator.And, Groups = [] });
		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task Build_WhenSinglePredicate_ReturnsPredicate()
	{
		var filter = new QueryFilter
		{
			Operator = FilterOperator.And,
			Groups = [new FilterGroup { Operator = FilterOperator.And, Predicates = ["Year >= 2020"] }],
		};

		var result = FilterEvaluator.Build(filter);

		await Assert.That(result).IsEqualTo("Year >= 2020");
	}

	[Test]
	public async Task Build_WhenMultipleAndPredicates_ReturnsAndExpression()
	{
		var filter = new QueryFilter
		{
			Operator = FilterOperator.And,
			Groups = [new FilterGroup { Operator = FilterOperator.And, Predicates = ["Year >= 2020", "Genre == \"Action\""] }],
		};

		var result = FilterEvaluator.Build(filter);

		await Assert.That(result).IsEqualTo("(Year >= 2020 and Genre == \"Action\")");
	}

	[Test]
	public async Task Build_WhenMultipleOrPredicates_ReturnsOrExpression()
	{
		var filter = new QueryFilter
		{
			Operator = FilterOperator.Or,
			Groups = [new FilterGroup { Operator = FilterOperator.Or, Predicates = ["Genre == \"Action\"", "Genre == \"Comedy\""] }],
		};

		var result = FilterEvaluator.Build(filter);

		await Assert.That(result).IsEqualTo("(Genre == \"Action\" or Genre == \"Comedy\")");
	}

	[Test]
	public async Task Build_WhenEmptyGroup_ReturnsNull()
	{
		var filter = new QueryFilter { Operator = FilterOperator.And, Groups = [new FilterGroup { Operator = FilterOperator.And, Predicates = [] }] };

		var result = FilterEvaluator.Build(filter);

		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task Build_WhenMultipleAndGroups_CombinesGroupsWithAnd()
	{
		var filter = new QueryFilter
		{
			Operator = FilterOperator.And,
			Groups =
			[
				new FilterGroup { Operator = FilterOperator.And, Predicates = ["Year >= 2020"] },
				new FilterGroup { Operator = FilterOperator.And, Predicates = ["Genre == \"Action\""] },
			],
		};

		var result = FilterEvaluator.Build(filter);

		await Assert.That(result).IsEqualTo("(Year >= 2020 and Genre == \"Action\")");
	}

	[Test]
	public async Task Build_WhenMultipleOrGroups_CombinesGroupsWithOr()
	{
		var filter = new QueryFilter
		{
			Operator = FilterOperator.Or,
			Groups =
			[
				new FilterGroup { Operator = FilterOperator.And, Predicates = ["Year >= 2020"] },
				new FilterGroup { Operator = FilterOperator.And, Predicates = ["Genre == \"Action\""] },
			],
		};

		var result = FilterEvaluator.Build(filter);

		await Assert.That(result).IsEqualTo("(Year >= 2020 or Genre == \"Action\")");
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
					Predicates = ["Year >= 2020"],
					Groups = [new FilterGroup { Operator = FilterOperator.Or, Predicates = ["Genre == \"Action\"", "Genre == \"Comedy\""] }],
				},
			],
		};

		var result = FilterEvaluator.Build(filter);

		await Assert.That(result).IsEqualTo("(Year >= 2020 and (Genre == \"Action\" or Genre == \"Comedy\"))");
	}

	[Test]
	public async Task Build_WhenGroupContainsOnlyEmptyNestedGroups_ReturnsNull()
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
}
