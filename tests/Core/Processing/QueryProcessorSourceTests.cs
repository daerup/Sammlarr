using Core.Processing;
using Domain.Processing;
using TestInfrastructure;
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
		var options = new QueryOptions { Filter = Some.Of(Filter(Group(String(nameof(LazyRow.Name), FilterPredicateOperator.Equals, "Keep")))) };
		var predicates = new QueryPredicates<LazyRow>(IsWantlisted: _ => false, IsBlocklisted: _ => false, IsOwned: _ => false, Identity: _ => Guid.Empty);

		var results = await _testee.ExecuteAsync(lazy, options, predicates).ToListAsync();

		await Assert.That(results.Select(row => row.Outcome.Case)).IsEquivalentTo([OutcomeCase.Qualified, OutcomeCase.Culled], CollectionOrdering.Matching);
	}
}

internal sealed record LazyRow(string Name);
