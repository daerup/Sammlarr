using Contracts.Processing;
using Core.Feed;
using CsCheck;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Processing;
using Domain.Providers;

namespace Core.Tests.Feed;

public class FeedRunPlanProperties
{
	private const int Threshold = 3;

	private sealed record Scenario(FeedRunPlan Plan, IReadOnlySet<MediaId> Owned, FeedStrategy Strategy, IReadOnlyDictionary<MediaId, int> Absences);

	private static readonly Gen<Scenario> Scenarios = Gen.Select(
			Gen.OneOfConst(QueryOutcome.Qualified, QueryOutcome.Culled, QueryOutcome.Truncated),
			Gen.Bool,
			Gen.Bool,
			Gen.Bool,
			Gen.Bool,
			Gen.Int[0, Threshold + 1],
			(outcome, owned, inWatchlist, inBlacklist, inSource, absences) => (outcome, owned, inWatchlist, inBlacklist, inSource, absences)
		)
		.Array[0, 6]
		.SelectMany(situations =>
			Gen.Select(
				Gen.OneOfConst<FeedStrategy>(new Append(), new Mirror(Threshold), new Mirror(Threshold)),
				Gen.OneOfConst(FeedSource.Complete, FeedSource.Complete, FeedSource.Complete, FeedSource.Partial),
				Gen.Int[0, 3],
				(strategy, source, unresolved) => Build(situations, strategy, source, unresolved)
			)
		);

	[Test]
	public void Compute_ForAnyScenario_OnlyDropsWhatTheFeedOwns() =>
		Scenarios.Sample(scenario => scenario.Plan.Delta.Drops.All(scenario.Owned.Contains), iter: 10_000);

	[Test]
	public void Compute_ForAnyScenario_NeverDropsSomethingItReportsAsKept() =>
		Scenarios.Sample(scenario => !scenario.Plan.Delta.Drops.Intersect(scenario.Plan.Delta.Keeps).Any(), iter: 10_000);

	[Test]
	public void Compute_ForAnyScenario_NeverAddsSomethingTheFeedAlreadyOwns() =>
		Scenarios.Sample(scenario => !scenario.Plan.Delta.Adds.Any(scenario.Owned.Contains), iter: 10_000);

	[Test]
	public void Compute_ForAnAppendFeed_NeverDrops() =>
		Scenarios.Sample(scenario => scenario.Strategy is not Append || scenario.Plan.Delta.Drops.Count == 0, iter: 10_000);

	[Test]
	public void Compute_WheneverInconclusive_DropsNothing() =>
		Scenarios.Sample(scenario => scenario.Plan.Conclusiveness is Conclusiveness.Conclusive || scenario.Plan.Delta.Drops.Count == 0, iter: 10_000);

	[Test]
	public void Compute_ForAnyScenario_AccountsForEveryOwnedItem() =>
		Scenarios.Sample(scenario => scenario.Plan.Delta.Keeps.Concat(scenario.Plan.RunsRemaining.Keys).ToHashSet().SetEquals(scenario.Owned), iter: 10_000);

	[Test]
	public void Compute_WhileGraceRemains_NeverDrops() =>
		Scenarios.Sample(scenario => scenario.Plan.Delta.Drops.All(id => scenario.Absences.GetValueOrDefault(id) + 1 >= Threshold), iter: 10_000);

	[Test]
	public void Compute_ForAnyMissingOwnedItem_CountsDownFromTheThreshold() =>
		Scenarios.Sample(
			scenario =>
				scenario.Strategy is not Mirror
				|| scenario.Plan.RunsRemaining.All(entry => entry.Value == Math.Max(0, Threshold - (scenario.Absences.GetValueOrDefault(entry.Key) + 1))),
			iter: 10_000
		);

	private static Scenario Build(
		(QueryOutcome Outcome, bool Owned, bool InWatchlist, bool InBlacklist, bool InSource, int Absences)[] situations,
		FeedStrategy strategy,
		FeedSource source,
		int unresolvedCount
	)
	{
		var verdicts = new List<QueryVerdict<MediaLookup>>();
		var owned = new HashSet<MediaId>();
		var absences = new Dictionary<MediaId, int>();

		for (var index = 0; index < situations.Length; index++)
		{
			var id = MediaId.From(new Guid(index + 1, 0, 0, new byte[8]));
			var (outcome, isOwned, inWatchlist, inBlacklist, inSource, absenceCount) = situations[index];

			if (isOwned)
			{
				owned.Add(id);
				absences[id] = absenceCount;
			}

			if (inSource)
			{
				var enrollment = new Enrollment
				{
					InWatchlist = inWatchlist,
					InBlacklist = inBlacklist,
					Owned = isOwned,
				};
				verdicts.Add(new QueryVerdict<MediaLookup>(Row(id), outcome, enrollment, ConsumesSlot: true));
			}
		}

		return new Scenario(FeedRunPlan.Compute(verdicts, unresolvedCount, owned, absences, strategy, source), owned, strategy, absences);
	}

	private static MediaLookup Row(MediaId id) =>
		new PartialMovie
		{
			Identity = new MediaIdentity(id, [TmdbId.From(id.Value.ToString(), MediaType.Movie)]),
			Title = "Title",
			Tagline = string.Empty,
			Summary = string.Empty,
			PosterUrl = string.Empty,
			BackdropUrl = string.Empty,
			ReleaseDate = new DateOnly(2020, 1, 1),
			Genres = [],
			Runtime = TimeSpan.FromMinutes(90),
			Rating = RatingSummary.None,
		}.ToMatch();
}
