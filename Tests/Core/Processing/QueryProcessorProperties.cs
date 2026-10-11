using Core.Processing;
using CsCheck;
using Domain.Processing;

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
		return [.. verdicts.Where(verdict => verdict.Outcome is QueryOutcome.Qualified).Select(verdict => verdict.Item.Id)];
	}
}
