using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;
using TestInfrastructure;
using UI.Features.Feeds.Components;
using UI.Shared.Changes;

namespace UI.Tests.Feeds;

public class FeedRunViewTests
{
	private static readonly Feed Comedy = TestFeeds.Feed(new Guid(1, 0, 0, new byte[8]), "Comedy");

	[Test]
	public async Task Problem_WhenARunFailedReadingTheSource_SaysTheSourceCouldNotBeRead()
	{
		var run = TestFeeds.Run(Comedy, Guid.NewGuid(), Conclusiveness.NoItems) with { SourceFailure = Some.Of("HTTP 404") };

		await Assert.That(FeedRunView.Problem(run)).IsEqualTo("The source could not be read, so nothing was applied.");
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
	[Arguments(FeedRun.Interrupted)]
	[Arguments(FeedRun.Cancelled)]
	public async Task Problem_ForARunThatStoppedBeforeItFinished_SaysSoRatherThanBlamingTheSource(string reason)
	{
		var run = TestFeeds.Run(Comedy, Guid.NewGuid(), Conclusiveness.Stopped) with { SourceFailure = Some.Of(reason) };

		await Assert.That(FeedRunView.Problem(run)).IsEqualTo("The run stopped before it finished, so some of its changes may be missing.");
	}

	[Test]
	public async Task Items_ForATitleNoSinkCouldAdd_ListItApartFromTheAddedOnes()
	{
		var added = MediaId.New();
		var stuck = MediaId.New();
		var run = TestFeeds.Run(Comedy, Guid.NewGuid(), Conclusiveness.Conclusive) with
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
	public async Task Problem_ForARunStillGoing_IsNull()
	{
		var run = TestFeeds.Run(Comedy, Guid.NewGuid(), Conclusiveness.Pending);

		await Assert.That(FeedRunView.Problem(run)).IsNull();
	}

	[Test]
	public async Task Problem_ForARunWithTitlesItCouldNotLookUp_SaysNoRemovalsWereApplied()
	{
		var run = TestFeeds.Run(Comedy, Guid.NewGuid(), Conclusiveness.Unresolved) with { Counts = FeedRunCounts.None with { Total = 3 } };

		await Assert.That(FeedRunView.Problem(run)).IsEqualTo("Some titles couldn't be looked up, so no removals were applied.");
	}

	[Test]
	public async Task Problem_WhenARunFailedWithoutASourceFailure_SaysTheSourceReturnedNothing()
	{
		var run = TestFeeds.Run(Comedy, Guid.NewGuid(), Conclusiveness.NoItems);

		await Assert.That(FeedRunView.Problem(run)).IsEqualTo("The source returned nothing, so nothing was applied.");
	}

	[Test]
	public async Task Problem_WhenARunStillLoadingFailedPartway_SaysTheSourceFailedPartway()
	{
		var run = TestFeeds.Run(Comedy, Guid.NewGuid(), Conclusiveness.StillLoading) with
		{
			Counts = FeedRunCounts.None with { Total = 3 },
			SourceFailure = Some.Of("HTTP 500"),
		};

		await Assert.That(FeedRunView.Problem(run)).IsEqualTo("The source failed partway through, so no removals were applied.");
	}

	[Test]
	public async Task Problem_WhenARunWasStillLoadingWithoutAFailure_SaysTheSourceWasStillLoading()
	{
		var run = TestFeeds.Run(Comedy, Guid.NewGuid(), Conclusiveness.StillLoading) with { Counts = FeedRunCounts.None with { Total = 3 } };

		await Assert.That(FeedRunView.Problem(run)).IsEqualTo("The source was still loading, so no removals were applied.");
	}

	[Test]
	public async Task Problem_WhenAPlanSourceFailedAfterLoadingSome_SaysTheRunWouldHoldBackRemovals()
	{
		var plan = new AppendPlan([], [new UnmatchedMedia([], new MovieDetails())], FeedDelta.None, Conclusiveness.StillLoading)
		{
			SourceFailure = Some.Of("HTTP 500"),
		};

		await Assert.That(FeedRunView.Problem(plan)).IsEqualTo("The source failed partway through, so this run would hold back removals.");
	}

	[Test]
	public async Task Problem_WhenAPlanHasNoSourceFailure_IsNull()
	{
		var plan = new AppendPlan([], [], FeedDelta.None, Conclusiveness.Conclusive);

		await Assert.That(FeedRunView.Problem(plan)).IsNull();
	}

	[Test]
	public async Task Problem_WhenAPlanSourceFailedBeforeLoadingAnything_SaysTheRunWouldChangeNothing()
	{
		var plan = new AppendPlan([], [], FeedDelta.None, Conclusiveness.NoItems) { SourceFailure = Some.Of("HTTP 404") };

		await Assert.That(FeedRunView.Problem(plan)).IsEqualTo("The source could not be read, so this run would change nothing.");
	}

	private static MatchedMedia Movie(MediaId id, string title)
	{
		return new(new MediaIdentity(id, []), new MovieDetails { Title = title });
	}
}
