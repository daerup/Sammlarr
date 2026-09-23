using Contracts.Processing;
using Core.Feed;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Processing;
using Domain.Providers;
using TestInfrastructure;

namespace Core.Tests.Feed;

public class FeedRunPlanTests
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
	public async Task Compute_WhenAnOwnedItemIsBlacklisted_StillDropsItOnceItStopsQualifying()
	{
		var plan = Compute(
			[Verdict(Kept, QueryOutcome.Qualified, owned: true), Verdict(Dropped, QueryOutcome.Culled, owned: true, inBlacklist: true)],
			owned: [Kept, Dropped]
		);

		await Assert.That(plan.Delta.Drops).IsEquivalentTo([Dropped]);
	}

	[Test]
	public async Task Compute_WhenAnOwnedBlacklistedItemStillQualifies_KeepsIt()
	{
		var plan = Compute([Verdict(Kept, QueryOutcome.Qualified, owned: true, inBlacklist: true)], owned: [Kept]);

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
		var plan = Compute([], owned: [Kept], unresolvedCount: 12);

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
		var plan = Compute([Verdict(Fresh, QueryOutcome.Qualified)], owned: [Kept, Dropped], completeness: Completeness.Partial);

		await Assert.That(plan.Conclusiveness).IsEqualTo(Conclusiveness.StillLoading);
		await Assert.That(plan.Delta.Adds).IsEquivalentTo([Fresh]);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenGraceRemains_DropsNothingButCountsDownTheRun()
	{
		var plan = Compute([Verdict(Kept, QueryOutcome.Qualified, owned: true)], owned: [Kept, Dropped], absences: []);

		await Assert.That(plan.Delta.Drops).IsEmpty();
		await Assert.That(plan.RunsRemaining[Dropped]).IsEqualTo(2);
	}

	[Test]
	public async Task Compute_WhenTheLastRunOfGraceIsSpent_DropsTheItem()
	{
		var plan = Compute([Verdict(Kept, QueryOutcome.Qualified, owned: true)], owned: [Kept, Dropped], absences: [(Dropped, 2)]);

		await Assert.That(plan.Delta.Drops).IsEquivalentTo([Dropped]);
		await Assert.That(plan.RunsRemaining[Dropped]).IsEqualTo(0);
	}

	[Test]
	public async Task Compute_ForAQualifyingOwnedItem_ReportsNoGraceCountdownAtAll()
	{
		var plan = Compute([Verdict(Kept, QueryOutcome.Qualified, owned: true)], owned: [Kept], absences: [(Kept, 2)]);

		await Assert.That(plan.RunsRemaining).DoesNotContainKey(Kept);
		await Assert.That(plan.Delta.Keeps).IsEquivalentTo([Kept]);
	}

	private static FeedRunPlan Compute(
		IReadOnlyList<QueryVerdict<MediaLookup>> verdicts,
		IReadOnlyList<MediaId> owned,
		FeedStrategy? strategy = null,
		Completeness completeness = Completeness.Complete,
		int unresolvedCount = 0,
		IReadOnlyList<(MediaId Id, int Absences)>? absences = null
	) =>
		FeedRunPlan.Compute(
			verdicts,
			unresolvedCount,
			owned.ToHashSet(),
			(absences ?? [.. owned.Select(id => (Id: id, Absences: 10))]).ToDictionary(entry => entry.Id, entry => entry.Absences),
			strategy ?? new Mirror(3),
			completeness
		);

	private static QueryVerdict<MediaLookup> Verdict(MediaId id, QueryOutcome outcome, bool owned = false, bool inBlacklist = false) =>
		new(
			Row(id),
			outcome,
			new Enrollment
			{
				InWatchlist = owned,
				InBlacklist = inBlacklist,
				Owned = owned,
			},
			ConsumesSlot: true
		);

	private static MediaId Id(int seed) => MediaId.From(new Guid(seed, 0, 0, new byte[8]));

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
