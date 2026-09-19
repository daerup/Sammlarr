using Contracts.Processing;
using Core.Processing;
using Domain.Processing;
using TestInfrastructure;
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

		var results = await _testee.ExecuteAsync(lazy, options, _ => false, _ => false).ToListAsync();

		await Assert.That(results).AreOfUnionType<IList<QueryResult<LazyRow>>, QueryResult<LazyRow>>(typeof(LazyRow), typeof(Culled<LazyRow>));
	}
}

internal record LazyRow(string Name);
