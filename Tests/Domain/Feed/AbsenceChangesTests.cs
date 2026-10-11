using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Media;
using Domain.Processing;
using TestInfrastructure;

namespace Domain.Tests.Feed;

public class AbsenceChangesTests
{
	private static readonly MediaId Listed = Id(1);
	private static readonly MediaId Gone = Id(2);

	[Test]
	public async Task Of_ForAnInconclusiveMirrorPlan_IsNone()
	{
		var plan = Plan(FeedLoadState.Unfinished, []);

		await Assert.That(AbsenceChanges.Of(plan, [])).IsNone();
	}

	[Test]
	public async Task Of_ForAnAppendPlan_IsNone()
	{
		var plan = FeedPlans.Compute(
			new ClassifiedFeedLoad([], [], FeedLoadState.Finished),
			new HashSet<MediaId> { Gone },
			new Dictionary<MediaId, int>(),
			new Append()
		);

		await Assert.That(AbsenceChanges.Of(plan, [])).IsNone();
	}

	[Test]
	public async Task Of_WhenARemovalFails_KeepsItsCount()
	{
		var plan = Plan(FeedLoadState.Finished, []);

		var changes = await Assert.That(AbsenceChanges.Of(plan, [])).IsSome();

		await Assert.That(plan.Delta.Drops).Contains(Gone);
		await Assert.That(changes!.Missed).IsEquivalentTo([Gone]);
	}

	[Test]
	public async Task Of_ForARemovedTitle_ForgetsIt()
	{
		var changes = await Assert.That(AbsenceChanges.Of(Plan(FeedLoadState.Finished, []), [Gone])).IsSome();

		await Assert.That(changes!.Missed).IsEmpty();
	}

	[Test]
	public async Task Of_ForAnOwnedTitleInConflict_CountsItSeen()
	{
		var conflict = new UnmatchedMedia([TmdbId.From("26177", MediaType.Movie)], new MovieDetails(), UnmatchedReason.Conflict) { ConflictsWith = [Gone] };

		var changes = await Assert.That(AbsenceChanges.Of(Plan(FeedLoadState.Finished, [conflict]), [])).IsSome();

		await Assert.That(changes!.Missed).IsEmpty();
	}

	private static IFeedPlan Plan(FeedLoadState state, IReadOnlyList<UnmatchedMedia> unmatched)
	{
		var listed = new QueryVerdict<MatchedMedia>(
			new MatchedMedia(new MediaIdentity(Listed, [TmdbId.From("550", MediaType.Movie)]), new MovieDetails { Title = "Fight Club" }),
			QueryOutcome.Qualified,
			new Enrollment
			{
				InWantlist = true,
				InBlocklist = false,
				Owned = true,
			},
			ConsumesSlot: true
		);
		return FeedPlans.Compute(
			new ClassifiedFeedLoad([listed], unmatched, state),
			new HashSet<MediaId> { Listed, Gone },
			new Dictionary<MediaId, int> { [Gone] = 2 },
			new Mirror(3)
		);
	}

	private static MediaId Id(int seed) => MediaId.From(new Guid(seed, 0, 0, new byte[8]));
}
