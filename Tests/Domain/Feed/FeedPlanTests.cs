using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Media;
using Domain.Processing;

namespace Domain.Tests.Feed;

public class FeedPlanTests
{
	private static readonly MediaId Kept = Id(1);
	private static readonly MediaId Dropped = Id(2);
	private static readonly MediaId Fresh = Id(3);

	[Test]
	public async Task Compute_ForAMirrorFeed_DropsAnOwnedItemThatVanishedFromTheSource()
	{
		var plan = Compute([Verdict(Kept, QueryOutcome.Qualified, owned: true)], owned: [Kept, Dropped]);

		await Assert.That(plan.Delta.Drops).IsEquivalentTo([Dropped]);
		await Assert.That(plan.Delta.Keeps).IsEquivalentTo([Kept]);
	}

	[Test]
	public async Task Compute_ForAMirrorFeed_DropsAnOwnedItemThatNoLongerQualifies()
	{
		var plan = Compute([Verdict(Kept, QueryOutcome.Qualified, owned: true), Verdict(Dropped, QueryOutcome.Culled, owned: true)], owned: [Kept, Dropped]);

		await Assert.That(plan.Delta.Drops).IsEquivalentTo([Dropped]);
	}

	[Test]
	public async Task Compute_ForAnAppendFeed_DropsNothingEvenWhenOwnedItemsVanish()
	{
		var plan = Compute([Verdict(Kept, QueryOutcome.Qualified, owned: true)], owned: [Kept, Dropped], strategy: new Append());

		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenAnOwnedItemIsBlocklisted_StillDropsItOnceItStopsQualifying()
	{
		var plan = Compute(
			[Verdict(Kept, QueryOutcome.Qualified, owned: true), Verdict(Dropped, QueryOutcome.Culled, owned: true, inBlocklist: true)],
			owned: [Kept, Dropped]
		);

		await Assert.That(plan.Delta.Drops).IsEquivalentTo([Dropped]);
	}

	[Test]
	public async Task Compute_WhenAnOwnedBlocklistedItemStillQualifies_KeepsIt()
	{
		var plan = Compute([Verdict(Kept, QueryOutcome.Qualified, owned: true, inBlocklist: true)], owned: [Kept]);

		await Assert.That(plan.Delta.Keeps).IsEquivalentTo([Kept]);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenAQualifyingItemIsNotEnrolled_AddsIt()
	{
		var plan = Compute([Verdict(Fresh, QueryOutcome.Qualified)], owned: []);

		await Assert.That(plan.Delta.Adds).IsEquivalentTo([Fresh]);
	}

	[Test]
	public async Task Compute_WhenTheSourceLoadedNothing_IsInconclusiveAndDropsNothing()
	{
		var plan = Compute([], owned: [Kept, Dropped]);

		await Assert.That(plan.Conclusiveness).IsEqualTo(Conclusiveness.NoItems);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenNothingResolvedInTheProvider_IsInconclusiveAndDropsNothing()
	{
		var plan = Compute([], owned: [Kept], unmatchedCount: 12);

		await Assert.That(plan.Conclusiveness).IsEqualTo(Conclusiveness.NoMatches);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenTheLimitAdmitsNothing_IsInconclusiveAndDropsNothing()
	{
		var plan = Compute([Verdict(Kept, QueryOutcome.Truncated, owned: true), Verdict(Fresh, QueryOutcome.Truncated)], owned: [Kept]);

		await Assert.That(plan.Conclusiveness).IsEqualTo(Conclusiveness.NoneAdmitted);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_WhileTheListIsStillLoading_DropsNothingButStillAdds()
	{
		var plan = Compute([Verdict(Fresh, QueryOutcome.Qualified)], owned: [Kept, Dropped], state: FeedLoadState.Unfinished);

		await Assert.That(plan.Conclusiveness).IsEqualTo(Conclusiveness.StillLoading);
		await Assert.That(plan.Delta.Adds).IsEquivalentTo([Fresh]);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenGraceRemains_DropsNothingButCountsTheMissedRun()
	{
		var plan = Mirrored([Verdict(Kept, QueryOutcome.Qualified, owned: true)], owned: [Kept, Dropped], absences: []);

		await Assert.That(plan.Delta.Drops).IsEmpty();
		await Assert.That(plan.MissedRuns[Dropped]).IsEqualTo(1);
	}

	[Test]
	public async Task Compute_WhenTheLastRunOfGraceIsSpent_DropsTheItem()
	{
		var plan = Mirrored([Verdict(Kept, QueryOutcome.Qualified, owned: true)], owned: [Kept, Dropped], absences: [(Dropped, 2)]);

		await Assert.That(plan.Delta.Drops).IsEquivalentTo([Dropped]);
		await Assert.That(plan.MissedRuns[Dropped]).IsEqualTo(3);
	}

	[Test]
	public async Task Compute_ForAQualifyingOwnedItem_ReportsNoGraceCountdownAtAll()
	{
		var plan = Mirrored([Verdict(Kept, QueryOutcome.Qualified, owned: true)], owned: [Kept], absences: [(Kept, 2)]);

		await Assert.That(plan.MissedRuns).DoesNotContainKey(Kept);
		await Assert.That(plan.Delta.Keeps).IsEquivalentTo([Kept]);
	}

	[Test]
	public async Task Compute_WhenATitleCouldNotBeLookedUp_IsUnresolvedAndDropsNothing()
	{
		var plan = Compute(
			[Verdict(Kept, QueryOutcome.Qualified, owned: true)],
			owned: [Kept, Dropped],
			unmatched: [Unmatched(1) with { Reason = UnmatchedReason.CouldNotLookUp }]
		);

		await Assert.That(plan.Conclusiveness).IsEqualTo(Conclusiveness.Unresolved);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_ForAnOwnedTitleInConflict_CountsNoAbsence()
	{
		var plan = Mirrored(
			[Verdict(Kept, QueryOutcome.Qualified, owned: true)],
			owned: [Kept, Dropped],
			absences: [(Dropped, 2)],
			unmatched: [Unmatched(1) with { Reason = UnmatchedReason.Conflict, ConflictsWith = [Dropped] }]
		);

		await Assert.That(plan.MissedRuns).DoesNotContainKey(Dropped);
		await Assert.That(plan.Delta.Keeps).Contains(Dropped);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_ForAStaleOwnedTitleThatNoLongerQualifies_CountsNoAbsence()
	{
		var stale = Verdict(Dropped, QueryOutcome.Culled, owned: true) with { Item = Row(Dropped) with { Stale = true } };

		var plan = Mirrored([Verdict(Kept, QueryOutcome.Qualified, owned: true), stale], owned: [Kept, Dropped], absences: [(Dropped, 2)]);

		await Assert.That(plan.MissedRuns).DoesNotContainKey(Dropped);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	private static IFeedPlan Compute(
		IReadOnlyList<QueryVerdict<MatchedMedia>> verdicts,
		IReadOnlyList<MediaId> owned,
		FeedStrategy? strategy = null,
		FeedLoadState state = FeedLoadState.Finished,
		int unmatchedCount = 0,
		IReadOnlyList<(MediaId Id, int Absences)>? absences = null,
		IReadOnlyList<UnmatchedMedia>? unmatched = null
	)
	{
		return FeedPlans.Compute(
			new ClassifiedFeedLoad(verdicts, unmatched ?? [.. Enumerable.Range(0, unmatchedCount).Select(Unmatched)], state),
			owned.ToHashSet(),
			(absences ?? [.. owned.Select(id => (Id: id, Absences: 10))]).ToDictionary(entry => entry.Id, entry => entry.Absences),
			strategy ?? new Mirror(3)
		);
	}

	private static MirrorPlan Mirrored(
		IReadOnlyList<QueryVerdict<MatchedMedia>> verdicts,
		IReadOnlyList<MediaId> owned,
		IReadOnlyList<(MediaId Id, int Absences)>? absences = null,
		IReadOnlyList<UnmatchedMedia>? unmatched = null
	)
	{
		return (MirrorPlan)Compute(verdicts, owned, absences: absences, unmatched: unmatched);
	}

	private static QueryVerdict<MatchedMedia> Verdict(MediaId id, QueryOutcome outcome, bool owned = false, bool inBlocklist = false)
	{
		return new(
			Row(id),
			outcome,
			new Enrollment
			{
				InWantlist = owned,
				InBlocklist = inBlocklist,
				Owned = owned,
			},
			ConsumesSlot: true
		);
	}

	private static MediaId Id(int seed) => MediaId.From(new Guid(seed, 0, 0, new byte[8]));

	private static UnmatchedMedia Unmatched(int seed) => new([TmdbId.From($"unmatched-{seed}", MediaType.Movie)], new MovieDetails());

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
