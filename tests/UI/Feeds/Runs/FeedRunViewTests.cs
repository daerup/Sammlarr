using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;
using TestInfrastructure;
using UI.Features.Feeds.Components.Runs;
using UI.Shared.Changes;

namespace UI.Tests.Feeds.Runs;

public class FeedRunViewTests
{
	private static readonly Feed Comedy = TestFeeds.Feed(new Guid(1, 0, 0, new byte[8]));

	[Test]
	[Arguments(Conclusiveness.NoItems, "HTTP 404", 0, "The source could not be read, so nothing was applied.")]
	[Arguments(Conclusiveness.NoItems, null, 0, "The source returned nothing, so nothing was applied.")]
	[Arguments(Conclusiveness.Stopped, FeedRun.Interrupted, 0, "The run stopped before it finished, so some of its changes may be missing.")]
	[Arguments(Conclusiveness.Stopped, FeedRun.Cancelled, 0, "The run stopped before it finished, so some of its changes may be missing.")]
	[Arguments(Conclusiveness.Unresolved, null, 3, "Some titles couldn't be looked up, so no removals were applied.")]
	[Arguments(Conclusiveness.StillLoading, "HTTP 500", 3, "The source failed partway through, so no removals were applied.")]
	[Arguments(Conclusiveness.StillLoading, null, 3, "The source was still loading, so no removals were applied.")]
	[Arguments(Conclusiveness.Pending, null, 0, null)]
	public async Task Problem_ForARun_SaysWhatKeptItFromApplyingEverythingOrNothingWhileItIsStillGoing(
		Conclusiveness conclusiveness,
		string? failure,
		int total,
		string? expected
	)
	{
		var run = TestFeeds.Run(Comedy, Guid.NewGuid(), conclusiveness) with
		{
			Counts = FeedRunCounts.None with { Total = total },
			SourceFailure = Optional.Of(failure),
		};

		await Assert.That(FeedRunView.Problem(run)).IsEqualTo(expected);
	}

	[Test]
	[Arguments(3, 0, 0, "Not found by any service")]
	[Arguments(2, 1, 0, "2 not found by any service, 1 could not be looked up (tried again next run)")]
	[Arguments(2, 0, 1, "2 not found by any service, 1 whose ids disagree with stored titles")]
	public async Task Chips_TheUnmatchedChip_SaysWhyTheTitlesStayedUnmatched(int notFound, int couldNotLookUp, int conflict, string tooltip)
	{
		var counts = FeedRunCounts.None with { UnmatchedNotFound = notFound, UnmatchedCouldNotLookUp = couldNotLookUp, UnmatchedConflict = conflict };

		var chip = FeedRunView.Chips(counts).Single(chip => chip.Icon == "circle-alert");

		await Assert.That((chip.Count, chip.Tooltip)).IsEqualTo((notFound + couldNotLookUp + conflict, tooltip));
	}

	[Test]
	public async Task Items_ForATitleNoSinkCouldAdd_ListItApartFromTheAddedOnes()
	{
		var added = MediaId.New();
		var stuck = MediaId.New();
		var run = TestFeeds.Run(Comedy, Guid.NewGuid()) with
		{
			Items =
			[
				new FeedRunItem { MediaId = added, State = FeedRunItemState.Added },
				new FeedRunItem { MediaId = stuck, State = FeedRunItemState.Unaddressable },
			],
		};

		var items = FeedRunView.Items(run, new Dictionary<MediaId, MatchedMedia> { [added] = Movie(added, "Fresh"), [stuck] = Movie(stuck, "Stuck") });

		await Assert.That(items.Select(item => (item.Title, item.Kind))).IsEquivalentTo([("Fresh", ChangeKind.Added), ("Stuck", ChangeKind.Unaddressable)]);
	}

	[Test]
	[Arguments(Conclusiveness.StillLoading, true, "HTTP 500", "The source failed partway through, so this run would hold back removals.")]
	[Arguments(Conclusiveness.NoItems, false, "HTTP 404", "The source could not be read, so this run would change nothing.")]
	[Arguments(Conclusiveness.Conclusive, false, null, null)]
	public async Task Problem_ForAPlan_SaysWhatASourceFailureWouldHoldBackOrNothingWithoutOne(
		Conclusiveness conclusiveness,
		bool hasUnmatched,
		string? failure,
		string? expected
	)
	{
		UnmatchedMedia[] unmatched = hasUnmatched ? [new UnmatchedMedia([], new MovieDetails())] : [];
		var plan = new AppendPlan([], unmatched, FeedDelta.None, conclusiveness) { SourceFailure = Optional.Of(failure) };

		await Assert.That(FeedRunView.Problem(plan)).IsEqualTo(expected);
	}

	private static MatchedMedia Movie(MediaId id, string title)
	{
		return new(new MediaIdentity(id, []), new MovieDetails { Title = title });
	}
}
