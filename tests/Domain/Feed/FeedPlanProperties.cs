using CsCheck;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Media;
using Domain.Processing;
using TestInfrastructure;

namespace Domain.Tests.Feed;

public class FeedPlanProperties
{
	private const int Threshold = 3;

	private sealed record Scenario(IFeedPlan Plan, IReadOnlySet<MediaId> Owned, FeedStrategy Strategy, IReadOnlyDictionary<MediaId, int> Absences);

	private static readonly Gen<Scenario> Scenarios = Gen.Select(
			Gen.OneOfConst(OutcomeCase.Qualified, OutcomeCase.Culled, OutcomeCase.Truncated),
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
				Gen.OneOfConst(FeedLoadState.Finished, FeedLoadState.Finished, FeedLoadState.Finished, FeedLoadState.Unfinished),
				Gen.Select(Gen.OneOfConst(UnmatchedReason.NotFound, UnmatchedReason.CouldNotLookUp, UnmatchedReason.Conflict), Gen.Int[0, 5]).Array[0, 3],
				(strategy, state, unmatched) => Build(situations, strategy, state, unmatched)
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
		(OutcomeCase Outcome, bool Owned, bool InWantlist, bool InBlocklist, bool InSource, int Absences)[] situations,
		FeedStrategy strategy,
		FeedLoadState state,
		(UnmatchedReason Reason, int ConflictsWith)[] unmatchedTitles
	)
	{
		var verdicts = new List<QueryVerdict<MatchedMedia>>();
		var owned = new HashSet<MediaId>();
		var absences = new Dictionary<MediaId, int>();

		for (var index = 0; index < situations.Length; index++)
		{
			var id = Id(index);
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
				verdicts.Add(new QueryVerdict<MatchedMedia>(Row(id), enrollment, outcome.Outcome));
			}
		}

		var unmatched = unmatchedTitles
			.Select((title, index) => Unmatched(index, title.Reason, title.Reason is UnmatchedReason.Conflict ? [Id(title.ConflictsWith)] : []))
			.ToList();

		return new Scenario(FeedPlans.Compute(new ClassifiedFeedLoad(verdicts, unmatched, state), owned, absences, strategy), owned, strategy, absences);
	}

	private static MediaId Id(int index) => MediaId.From(new Guid(index + 1, 0, 0, new byte[8]));

	private static UnmatchedMedia Unmatched(int index, UnmatchedReason reason, ValueSet<MediaId> conflictsWith)
	{
		return new UnmatchedMedia([TmdbId.From($"unmatched-{index}", MediaType.Movie)], new MovieDetails(), reason) { ConflictsWith = conflictsWith };
	}

	private static MatchedMedia Row(MediaId id)
	{
		return new MatchedMedia(
			new MediaIdentity(id, [TmdbId.From(id.Value.ToString(), MediaType.Movie)]),
			new MovieDetails
			{
				Title = "Title",
				ReleaseDate = new DateOnly(2020, 1, 1),
				Runtime = TimeSpan.FromMinutes(90),
			}
		);
	}
}
