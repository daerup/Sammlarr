using Contracts.Processing;
using Core.Processing;
using Domain.Processing;
using TUnit.Assertions.Enums;
using static TestInfrastructure.QueryFilterFactory;

namespace Core.Tests.Processing;

public class QueryProcessorSourceTests
{
	private readonly QueryProcessor<LazyRow> _testee = new();

	[Test]
	public async Task Execute_WhenTheSourceIsLazyAndFiltered_ClassifiesAgainstASingleMaterialisation()
	{
		var lazy = new[] { "Keep", "Drop" }.Select(name => new LazyRow(name));
		var options = new QueryOptions { Filter = Filter(Group(String(nameof(LazyRow.Name), FilterPredicateOperator.Equals, "Keep"))) };
		var predicates = new QueryPredicates<LazyRow>(IsWantlisted: _ => false, IsBlocklisted: _ => false, IsOwned: _ => false);

		var results = await _testee.ExecuteAsync(lazy, options, predicates).ToListAsync();

		await Assert.That(results.Select(row => row.Outcome)).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Culled], CollectionOrdering.Matching);
	}
}

internal record LazyRow(string Name);
