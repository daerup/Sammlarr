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
		var plan = Compute([Verdict(Kept, QueryOutcome.Qualified, owned: true)], owned: [Owned(Kept), Owned(Dropped)]);

		await Assert.That(plan.Delta.Drops).IsEquivalentTo([Dropped]);
		await Assert.That(plan.Delta.Keeps).IsEquivalentTo([Kept]);
	}

	[Test]
	public async Task Compute_ForAMirrorFeed_DropsAnOwnedItemThatNoLongerQualifies()
	{
		var plan = Compute(
			[Verdict(Kept, QueryOutcome.Qualified, owned: true), Verdict(Dropped, QueryOutcome.Culled, owned: true)],
			owned: [Owned(Kept), Owned(Dropped)]
		);

		await Assert.That(plan.Delta.Drops).IsEquivalentTo([Dropped]);
	}

	[Test]
	public async Task Compute_ForAnAppendFeed_DropsNothingEvenWhenOwnedItemsVanish()
	{
		var plan = Compute([Verdict(Kept, QueryOutcome.Qualified, owned: true)], owned: [Owned(Kept), Owned(Dropped)], strategy: new Append());

		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenAnOwnedItemIsBlacklisted_WaivesItInsteadOfDroppingIt()
	{
		var plan = Compute(
			[Verdict(Kept, QueryOutcome.Qualified), Verdict(Dropped, QueryOutcome.Culled, owned: true, inBlacklist: true)],
			owned: [Owned(Dropped, inBlacklist: true)]
		);

		await Assert.That(plan.Delta.Waives).IsEquivalentTo([Dropped]);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenAnOwnedBlacklistedItemVanishedEntirely_WaivesItRatherThanDroppingIt()
	{
		var plan = Compute([Verdict(Kept, QueryOutcome.Qualified, owned: true)], owned: [Owned(Kept), Owned(Dropped, inBlacklist: true)]);

		await Assert.That(plan.Delta.Waives).IsEquivalentTo([Dropped]);
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
		var plan = Compute([], owned: [Owned(Kept), Owned(Dropped)]);

		await Assert.That(plan.Conclusiveness).IsEqualTo(Conclusiveness.NoItems);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenNothingResolvedInTheProvider_IsInconclusiveAndDropsNothing()
	{
		var plan = Compute([], owned: [Owned(Kept)], unresolvedCount: 12);

		await Assert.That(plan.Conclusiveness).IsEqualTo(Conclusiveness.NoMatches);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenTheLimitAdmitsNothing_IsInconclusiveAndDropsNothing()
	{
		var plan = Compute([Verdict(Kept, QueryOutcome.Truncated, owned: true), Verdict(Fresh, QueryOutcome.Truncated)], owned: [Owned(Kept)]);

		await Assert.That(plan.Conclusiveness).IsEqualTo(Conclusiveness.NoneAdmitted);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_WhileTheSourceIsStillLoading_DropsNothingButStillAddsAndWaives()
	{
		var plan = Compute([Verdict(Fresh, QueryOutcome.Qualified)], owned: [Owned(Kept), Owned(Dropped, inBlacklist: true)], source: FeedSource.Partial);

		await Assert.That(plan.Conclusiveness).IsEqualTo(Conclusiveness.StillLoading);
		await Assert.That(plan.Delta.Adds).IsEquivalentTo([Fresh]);
		await Assert.That(plan.Delta.Waives).IsEquivalentTo([Dropped]);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenTheLimitAdmitsNothing_StillWaivesABlacklistedOwnedItem()
	{
		var plan = Compute([Verdict(Kept, QueryOutcome.Truncated)], owned: [Owned(Dropped, inBlacklist: true)]);

		await Assert.That(plan.Conclusiveness).IsEqualTo(Conclusiveness.NoneAdmitted);
		await Assert.That(plan.Delta.Waives).IsEquivalentTo([Dropped]);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task Compute_WhenAnOwnedBlacklistedItemStillQualifies_WaivesItRatherThanReportingItKept()
	{
		var plan = Compute([Verdict(Dropped, QueryOutcome.Qualified, owned: true, inBlacklist: true)], owned: [Owned(Dropped, inBlacklist: true)]);

		await Assert.That(plan.Delta.Waives).IsEquivalentTo([Dropped]);
		await Assert.That(plan.Delta.Keeps).IsEmpty();
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	private static FeedRunPlan Compute(
		IReadOnlyList<QueryVerdict<MediaLookup>> verdicts,
		IReadOnlyList<(MediaId Id, bool InBlacklist)> owned,
		FeedStrategy? strategy = null,
		FeedSource source = FeedSource.Complete,
		int unresolvedCount = 0
	) => FeedRunPlan.Compute(verdicts, unresolvedCount, Ownership(owned), strategy ?? new Mirror(3), source);

	private static FeedOwnership Ownership(IReadOnlyList<(MediaId Id, bool InBlacklist)> owned) =>
		new(owned.Select(item => item.Id).ToHashSet(), owned.Where(item => item.InBlacklist).Select(item => item.Id).ToHashSet());

	private static (MediaId Id, bool InBlacklist) Owned(MediaId id, bool inBlacklist = false) => (id, inBlacklist);

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
