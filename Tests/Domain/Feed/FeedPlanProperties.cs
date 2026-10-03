using CsCheck;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Processing;
using Domain.Providers;

namespace Domain.Tests.Feed;

public class FeedPlanProperties
{
	private const int Threshold = 3;

	private sealed record Scenario(IFeedPlan Plan, IReadOnlySet<MediaId> Owned, FeedStrategy Strategy, IReadOnlyDictionary<MediaId, int> Absences);

	private static readonly Gen<Scenario> Scenarios = Gen.Select(
			Gen.OneOfConst(QueryOutcome.Qualified, QueryOutcome.Culled, QueryOutcome.Truncated),
			Gen.Bool,
			Gen.Bool,
			Gen.Bool,
			Gen.Bool,
			Gen.Int[0, Threshold + 1],
			(outcome, owned, inWantlist, inBlocklist, inSource, absences) => (outcome, owned, inWantlist, inBlocklist, inSource, absences)
		)
		.Array[0, 6]
		.SelectMany(situations =>
			Gen.Select(
				Gen.OneOfConst<FeedStrategy>(new Append(), new Mirror(Threshold), new Mirror(Threshold)),
				Gen.OneOfConst(Completeness.Complete, Completeness.Complete, Completeness.Complete, Completeness.Partial),
				Gen.Int[0, 3],
				(strategy, completeness, unmatched) => Build(situations, strategy, completeness, unmatched)
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
	public void Compute_ForAnyMirrorScenario_AccountsForEveryOwnedItem() =>
		Scenarios.Sample(
			scenario => scenario.Plan is not MirrorPlan mirror || mirror.Delta.Keeps.Concat(mirror.MissedRuns.Keys).ToHashSet().SetEquals(scenario.Owned),
			iter: 10_000
		);

	[Test]
	public void Compute_ForAnAppendFeed_ReportsNothingAsMissing() =>
		Scenarios.Sample(scenario => scenario.Strategy is not Append || scenario.Plan is AppendPlan, iter: 10_000);

	[Test]
	public void Compute_ForAnyScenario_OnlyKeepsWhatTheFeedOwns() =>
		Scenarios.Sample(scenario => scenario.Plan.Delta.Keeps.All(scenario.Owned.Contains), iter: 10_000);

	[Test]
	public void Compute_WhileGraceRemains_NeverDrops() =>
		Scenarios.Sample(scenario => scenario.Plan.Delta.Drops.All(id => scenario.Absences.GetValueOrDefault(id) + 1 >= Threshold), iter: 10_000);

	[Test]
	public void Compute_ForAnyMissingOwnedItem_CountsTheRunsItHasBeenMissing() =>
		Scenarios.Sample(
			scenario => scenario.Plan is not MirrorPlan mirror || mirror.MissedRuns.All(entry => entry.Value == scenario.Absences.GetValueOrDefault(entry.Key) + 1),
			iter: 10_000
		);

	private static Scenario Build(
		(QueryOutcome Outcome, bool Owned, bool InWantlist, bool InBlocklist, bool InSource, int Absences)[] situations,
		FeedStrategy strategy,
		Completeness completeness,
		int unmatchedCount
	)
	{
		var verdicts = new List<QueryVerdict<IdentifiedMedia>>();
		var owned = new HashSet<MediaId>();
		var absences = new Dictionary<MediaId, int>();

		for (var index = 0; index < situations.Length; index++)
		{
			var id = MediaId.From(new Guid(index + 1, 0, 0, new byte[8]));
			var (outcome, isOwned, inWantlist, inBlocklist, inSource, absenceCount) = situations[index];

			if (isOwned)
			{
				owned.Add(id);
				absences[id] = absenceCount;
			}

			if (inSource)
			{
				var enrollment = new Enrollment
				{
					InWantlist = inWantlist,
					InBlocklist = inBlocklist,
					Owned = isOwned,
				};
				verdicts.Add(new QueryVerdict<IdentifiedMedia>(Row(id), outcome, enrollment, ConsumesSlot: true));
			}
		}

		var unmatched = Enumerable
			.Range(0, unmatchedCount)
			.Select(index => new UnmatchedMedia([TmdbId.From($"unmatched-{index}", MediaType.Movie)], new MovieDetails()))
			.ToList();

		return new Scenario(FeedPlans.Compute(new ClassifiedFeedLoad(verdicts, unmatched, completeness), owned, absences, strategy), owned, strategy, absences);
	}

	private static IdentifiedMedia Row(MediaId id)
	{
		return new IdentifiedMedia(
			new MediaIdentity(id, [TmdbId.From(id.Value.ToString(), MediaType.Movie)]),
			new MovieDetails
			{
				Title = "Title",
				Tagline = string.Empty,
				Summary = string.Empty,
				PosterUrl = string.Empty,
				BackdropUrl = string.Empty,
				ReleaseDate = new DateOnly(2020, 1, 1),
				Runtime = TimeSpan.FromMinutes(90),
			}
		);
	}
}
