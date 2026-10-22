using Core.Processing;
using CsCheck;
using Domain.Processing;
using TestInfrastructure;

namespace Core.Tests.Processing;

public class QueryProcessorProperties
{
	private sealed record Scenario(Guid Seed, int Count, Title[] Titles);

	private sealed record Title(Guid Id, bool Owned, int Order);

	private static readonly Gen<Scenario> Scenarios = Gen.Select(
		Gen.Guid,
		Gen.Int[1, 8],
		Gen.Select(Gen.Bool, Gen.Int).Array[0, 20],
		(seed, count, titles) =>
			new Scenario(seed, count, [.. titles.Select((title, index) => new Title(new Guid(index + 1, 0, 0, new byte[8]), title.Item1, title.Item2))])
	);

	[Test]
	public Task Execute_ForARandomPick_PicksTheSameTitlesWhateverTheSourceOrder() =>
		Scenarios.SampleAsync(async scenario =>
		{
			var picked = await PickedAsync(scenario.Titles, scenario);
			var reordered = await PickedAsync(scenario.Titles.OrderBy(title => title.Order), scenario);
			return picked.SetEquals(reordered);
		});

	[Test]
	public Task Execute_ForEveryCulledTitle_ItsChecksFailTheFilter() =>
		Filtered.SampleAsync(async scenario =>
		{
			var options = new QueryOptions { Filter = Some.Of(scenario.Filter) };
			var predicates = new QueryPredicates<Pair>(IsWantlisted: _ => false, IsBlocklisted: _ => false, IsOwned: _ => false, Identity: pair => pair.Id);
			var verdicts = await new QueryProcessor<Pair>().ExecuteAsync(scenario.Pairs, options, predicates).ToListAsync();
			return verdicts.All(verdict =>
				verdict.Outcome switch
				{
					Culled culled => culled.Checks.Fails(scenario.Filter),
					Qualified or Truncated => true,
				}
			);
		});

	private sealed record Pair(Guid Id, int A, int B);

	private sealed record FilteredScenario(QueryFilter Filter, Pair[] Pairs);

	private static readonly Gen<FilterPredicate> Rules = Gen.Select(
		Gen.OneOfConst(nameof(Pair.A), nameof(Pair.B)),
		Gen.OneOfConst(FilterPredicateOperator.GreaterThan, FilterPredicateOperator.LessThan, FilterPredicateOperator.Between),
		Gen.Int[0, 9],
		(field, op, bound) => QueryFilterFactory.Number(field, op, bound)
	);

	private static readonly Gen<FilteredScenario> Filtered = Gen.Select(
		Gen.OneOfConst(FilterOperator.And, FilterOperator.Or),
		Rules.Array[0, 3],
		Rules.Array[0, 3],
		Gen.Select(Gen.Int[0, 9], Gen.Int[0, 9]).Array[1, 10],
		(op, all, any, values) =>
			new FilteredScenario(
				QueryFilterFactory.Filter(
					QueryFilterFactory.Group(op, [], [QueryFilterFactory.Group(FilterOperator.And, all), QueryFilterFactory.Group(FilterOperator.Or, any)])
				),
				[.. values.Select((value, index) => new Pair(new Guid(index + 1, 0, 0, new byte[8]), value.Item1, value.Item2))]
			)
	);

	private static async Task<HashSet<Guid>> PickedAsync(IEnumerable<Title> titles, Scenario scenario)
	{
		var options = new QueryOptions { Limit = Some.Of(new QueryLimit { Count = scenario.Count, Pick = LimitPick.Random }), ShuffleSeed = scenario.Seed };
		var predicates = new QueryPredicates<Title>(
			IsWantlisted: _ => false,
			IsBlocklisted: _ => false,
			IsOwned: title => title.Owned,
			Identity: title => title.Id
		);
		var verdicts = await new QueryProcessor<Title>().ExecuteAsync(titles, options, predicates).ToListAsync();
		return [.. verdicts.Where(verdict => verdict.Outcome is Qualified).Select(verdict => verdict.Item.Id)];
	}
}
