using System.ComponentModel;
using System.Runtime.CompilerServices;
using Contracts.Processing;
using Core.Processing;
using Shared;
using Test.Shared;
using TUnit.Assertions.Core;
using TUnit.Assertions.Enums;

namespace Core.Tests.Unit.Processing;

public class QueryProcessorTests
{
	private readonly QueryProcessor<Item> _testee = new();

	[Test]
	public async Task Execute_WhenNoOptionsProvided_ReturnsAllItems()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama") };

		var results = _testee.Execute(items, new QueryOptions()).ToList();
		await Assert.That(results).AreOfUnionType(typeof(Item), typeof(Item));
	}

	[Test]
	public async Task Execute_WhenLimitProvided_ReturnsWithSkippedUnion()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama") };
		var options = new QueryOptions { Limit = 1 };

		var results = _testee.Execute(items, options).ToList();

		await Assert.That(results).AreOfUnionType(typeof(Item), typeof(Skipped<Item>));
	}

	[Test]
	public async Task Execute_WhenSortProvided_ReturnsAllItemsAsValues()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama") };
		var options = new QueryOptions
		{
			Sort = new QuerySort { Property = nameof(Item.Year), Direction = ListSortDirection.Descending },
		};

		var results = _testee.Execute(items, options).ToList();
		await Assert.That(results).AreOfUnionType<IList<QueryResult<Item>>, QueryResult<Item>>(typeof(Item), typeof(Item));
		await Assert.That(results.OfUnionType<Item>()).IsEquivalentTo([items[1], items[0]], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Execute_WhenSortProvided_ReturnsAllItemsAsValuesWithSortDirection()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama"), new Item("C", 2019, "Comedy") };
		var options = new QueryOptions
		{
			Limit = 2,
			Sort = new QuerySort { Property = nameof(Item.Year), Direction = ListSortDirection.Ascending },
		};

		var results = _testee.Execute(items, options).ToList();
		await Assert.That(results).AreOfUnionType(typeof(Item), typeof(Item), typeof(Skipped<Item>));
		await Assert.That(results.OfUnionType<Item>()).IsEquivalentTo([items[2], items[0]], CollectionOrdering.Matching);
		await Assert.That(results.OfUnionType<Skipped<Item>>().Select(i => i.Item)).IsEquivalentTo([items[1]]);
	}

	[Test]
	public async Task Execute_WhenFilterProvided_ReturnsOnlyMatchingFilter()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama"), new Item("C", 2019, "Comedy") };
		var options = new QueryOptions
		{
			Filter = new QueryFilter
			{
				Operator = FilterOperator.And,
				Groups = [new FilterGroup { Operator = FilterOperator.And, Predicates = ["Genre == \"Drama\""] }],
			},
		};

		var results = _testee.Execute(items, options).ToList();
		await Assert.That(results).AreOfUnionType(typeof(Filtered<Item>), typeof(Item), typeof(Filtered<Item>));
	}

	[Test]
	public async Task Execute_WhenOrFilterProvided_ReturnsOnlyMatchingOrFilter()
	{
		var items = new[] { new Item("A", 2020, "Action"), new Item("B", 2021, "Drama"), new Item("C", 2019, "Comedy") };
		var options = new QueryOptions
		{
			Filter = new QueryFilter
			{
				Operator = FilterOperator.Or,
				Groups =
				[
					new FilterGroup { Operator = FilterOperator.And, Predicates = ["Genre == \"Drama\""] },
					new FilterGroup { Operator = FilterOperator.And, Predicates = ["Year <= 2019"] },
				],
			},
		};
		var results = _testee.Execute(items, options).ToList();
		await Assert.That(results).AreOfUnionType(typeof(Filtered<Item>), typeof(Item), typeof(Item));
	}
}

internal record Item(string Name, int Year, string Genre = "Unknown");

file static class Extension
{
	public static IEnumerable<TOut> OfUnionType<TOut>(this IEnumerable<QueryResult<Item>> source)
	{
		return Shared.EnumerableExtensions.OfUnionType<QueryResult<Item>, TOut>(source);
	}

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
