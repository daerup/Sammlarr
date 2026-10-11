using Contracts.Feed;
using DB.Data.Feed;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;
using Domain.Usage;
using Harness;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FeedRunLogTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private const int Window = 3;

	private static readonly MediaId Missing = MediaId.From(new Guid(1, 0, 0, new byte[8]));
	private static readonly MediaId Held = MediaId.From(new Guid(2, 0, 0, new byte[8]));

	private IFeedRepository _testee = null!;

	[Before(Test)]
	public void Setup() => _testee = new FeedRepository(fixture.ContextFactory);

	[Test]
	public async Task CompleteRunAsync_ForATitleMissingAcrossRuns_CountsTheConsecutiveRuns()
	{
		var feedId = await SeedFeedAsync();
		var missing = await fixture.SeedMediaAsync(TmdbId.From("550", MediaType.Movie));

		await CompleteAsync(feedId, 1, Some.Of(Missed(missing)));
		await CompleteAsync(feedId, 2, Some.Of(Missed(missing)));

		await Assert.That(await _testee.GetAbsencesAsync(feedId)).IsEquivalentTo(new Dictionary<MediaId, int> { [missing] = 2 });
	}

	[Test]
	public async Task CompleteRunAsync_WhenAMissingTitleComesBack_RestartsItsCount()
	{
		var feedId = await SeedFeedAsync();
		var missing = await fixture.SeedMediaAsync(TmdbId.From("550", MediaType.Movie));

		await CompleteAsync(feedId, 1, Some.Of(Missed(missing)));
		await CompleteAsync(feedId, 2, Some.Of(Missed()));
		await CompleteAsync(feedId, 3, Some.Of(Missed(missing)));

		await Assert.That(await _testee.GetAbsencesAsync(feedId)).IsEquivalentTo(new Dictionary<MediaId, int> { [missing] = 1 });
	}

	[Test]
	public async Task CompleteRunAsync_WithoutAbsenceChanges_LeavesTheCountsAlone()
	{
		var feedId = await SeedFeedAsync();
		var missing = await fixture.SeedMediaAsync(TmdbId.From("550", MediaType.Movie));

		await CompleteAsync(feedId, 1, Some.Of(Missed(missing)));
		await CompleteAsync(feedId, 2, new None());

		await Assert.That(await _testee.GetAbsencesAsync(feedId)).IsEquivalentTo(new Dictionary<MediaId, int> { [missing] = 1 });
	}

	[Test]
	public async Task CompleteRunAsync_ForATitleNoLongerOwned_DeletesItsRow()
	{
		var feedId = await SeedFeedAsync();
		var dropped = await fixture.SeedMediaAsync(TmdbId.From("550", MediaType.Movie));
		var stillMissing = await fixture.SeedMediaAsync(TmdbId.From("27205", MediaType.Movie));

		await CompleteAsync(feedId, 1, Some.Of(Missed(dropped, stillMissing)));
		await CompleteAsync(feedId, 2, Some.Of(Missed(stillMissing)));

		await Assert.That(await _testee.GetAbsencesAsync(feedId)).IsEquivalentTo(new Dictionary<MediaId, int> { [stillMissing] = 2 });
	}

	[Test]
	public async Task UpdateAsync_ForAMirrorFeedSwitchedToAppend_StartsItsCountsAgain()
	{
		var feedId = await SeedFeedAsync();
		var missing = await fixture.SeedMediaAsync(TmdbId.From("550", MediaType.Movie));
		await CompleteAsync(feedId, 1, Some.Of(Missed(missing)));

		await _testee.UpdateAsync(
			feedId,
			new FeedUpdateData
			{
				Name = "Comedy",
				IsEnabled = true,
				Provider = FeedProvider.Mdblist,
				Strategy = new FeedStrategy(new Append()),
				Identifier = "hdlists/comedy",
				Schedule = new FeedRunTrigger(new Manual()),
			}
		);

		await Assert.That(await _testee.GetAbsencesAsync(feedId)).IsEmpty();
	}

	[Test]
	public async Task RecordAsync_ForEveryRun_StoresTheCountsAndTheItemStates()
	{
		var feedId = await SeedFeedAsync();

		await _testee.StartRunAsync(
			Run(feedId, 1) with
			{
				Counts = new FeedRunCounts
				{
					Total = 182,
					Added = 1,
					Kept = 178,
					Bypassed = 2,
					Filtered = 12,
					Skipped = 4,
					UnmatchedNotFound = 2,
					UnmatchedCouldNotLookUp = 0,
					UnmatchedConflict = 0,
					Removed = 1,
					Known = 0,
					NotFoundEarlier = 0,
				},
				Items = [Item(Held, FeedRunItemState.Added), Item(Missing, FeedRunItemState.Removed)],
			}
		);

		var run = (await _testee.GetRunsAsync(RunQuery.Take(10) with { Feed = Some.Of(feedId) })).Single();

		await Assert.That(run.Counts.Kept).IsEqualTo(178);
		await Assert.That(run.Counts.Filtered).IsEqualTo(12);
		await Assert.That(run.Items.Single(item => item.State is FeedRunItemState.Removed).MediaId).IsEqualTo(Missing);
	}

	[Test]
	public async Task CompleteRunAsync_WithKnownCounts_ReadsThemBack()
	{
		var feedId = await SeedFeedAsync();
		var run = Run(feedId, 1);
		await _testee.StartRunAsync(run);

		await _testee.CompleteRunAsync(run with { Counts = run.Counts with { Known = 40, NotFoundEarlier = 3, UnmatchedNotFound = 5 } }, new None());

		var stored = (await _testee.GetRunsAsync(RunQuery.Take(10) with { Feed = Some.Of(feedId) })).Single();
		var lean = (await _testee.GetRunsAsync(RunQuery.Take(10) with { Feed = Some.Of(feedId), WithItems = false })).Single();
		await Assert.That((stored.Counts.Known, stored.Counts.NotFoundEarlier, stored.Counts.UnmatchedNotFound)).IsEqualTo((40, 3, 5));
		await Assert.That((lean.Counts.Known, lean.Counts.NotFoundEarlier)).IsEqualTo((40, 3));
	}

	[Test]
	public async Task CompleteRunAsync_WithItsCallsAnswerTimesAndTheUnmatchedSplit_ReadsThemBack()
	{
		var feedId = await SeedFeedAsync();
		var run = Run(feedId, 1);
		await _testee.StartRunAsync(run);
		FeedRunCalls[] calls = [new(RemoteService.Tmdb, UsageOutcome.Answered, 290), new(RemoteService.Plex, UsageOutcome.AnsweredAfter429, 37)];
		FeedRunTimes[] times = [new(RemoteService.Tmdb, 100, 280), new(RemoteService.Tmdb, 500, 10), new(RemoteService.Plex, 250, 74)];

		await _testee.CompleteRunAsync(
			run with
			{
				Calls = calls,
				Times = times,
				Counts = run.Counts with { UnmatchedNotFound = 2, UnmatchedCouldNotLookUp = 1 },
			},
			new None()
		);

		var stored = (await _testee.GetRunsAsync(RunQuery.Take(10) with { Feed = Some.Of(feedId) })).Single();
		await Assert.That(stored.Calls).IsEquivalentTo(calls);
		await Assert.That(stored.Times).IsEquivalentTo(times);
		await Assert.That((stored.Counts.UnmatchedNotFound, stored.Counts.UnmatchedCouldNotLookUp, stored.Counts.Unmatched)).IsEqualTo((2, 1, 3));

		var lean = (await _testee.GetRunsAsync(RunQuery.Take(10) with { Feed = Some.Of(feedId), WithItems = false })).Single();
		await Assert.That(lean.Items).IsEmpty();
		await Assert.That(lean.Calls).IsEquivalentTo(calls);
		await Assert.That(lean.Times).IsEquivalentTo(times);
	}

	[Test]
	public async Task GetRunsAsync_ForRunsWithAndWithoutASourceFailure_ReadsBothBack()
	{
		var feedId = await SeedFeedAsync();
		await _testee.StartRunAsync(Run(feedId, 1) with { SourceFailure = Some.Of("No list found.") });
		await _testee.StartRunAsync(Run(feedId, 2));

		var runs = await _testee.GetRunsAsync(RunQuery.Take(10) with { Feed = Some.Of(feedId) });

		await Assert.That(runs.Single(run => run.StartedAt == Day(1)).SourceFailure).IsSome().And.IsEqualTo("No list found.");
		await Assert.That(runs.Single(run => run.StartedAt == Day(2)).SourceFailure).IsNone();
	}

	[Test]
	public async Task GetRunsAsync_ForOneFeed_ReturnsItsNewestRunsFirstUpToTheLimit()
	{
		var comedy = await SeedFeedAsync();
		var horror = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(comedy, 1));
		await _testee.StartRunAsync(Run(comedy, 2));
		await _testee.StartRunAsync(Run(comedy, 3));
		await _testee.StartRunAsync(Run(horror, 4));

		var runs = await _testee.GetRunsAsync(RunQuery.Take(2) with { Feed = Some.Of(comedy) });

		await Assert.That(runs.Select(run => run.StartedAt)).IsEquivalentTo([Day(3), Day(2)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetRunsAsync_SinceACutoff_ReturnsEveryFeedsRunsFromThenNewestFirst()
	{
		var comedy = await SeedFeedAsync();
		var horror = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(comedy, 1));
		await _testee.StartRunAsync(Run(comedy, 3));
		await _testee.StartRunAsync(Run(horror, 2));

		var runs = await _testee.GetRunsAsync(RunQuery.Take(10) with { Since = Some.Of(Day(2)) });

		await Assert.That(runs.Select(run => run.FeedId)).IsEquivalentTo([comedy, horror], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetRunsAsync_ForACutoffInAnotherOffset_ComparesTheSameInstant()
	{
		var feedId = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(feedId, 2));

		var runs = await _testee.GetRunsAsync(RunQuery.Take(10) with { Since = Some.Of(Day(2).ToOffset(TimeSpan.FromHours(2))) });

		await Assert.That(runs).HasSingleItem();
	}

	[Test]
	public async Task GetRunsAsync_WhenTheRunToIncludeIsOlderThanTheLimit_KeepsGoingUntilItIsInTheList()
	{
		var feedId = await SeedFeedAsync();
		var oldest = Run(feedId, 1);

		await _testee.StartRunAsync(oldest);
		await _testee.StartRunAsync(Run(feedId, 2));
		await _testee.StartRunAsync(Run(feedId, 3));
		await _testee.StartRunAsync(Run(feedId, 4));

		var runs = await _testee.GetRunsAsync(RunQuery.Take(2) with { Feed = Some.Of(feedId), MustInclude = Some.Of(oldest.Id) });

		await Assert.That(runs.Select(run => run.StartedAt)).IsEquivalentTo([Day(4), Day(3), Day(2), Day(1)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetRunsAsync_WhenTheRunToIncludeIsUnknown_ReturnsThePlainLimit()
	{
		var feedId = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(feedId, 1));
		await _testee.StartRunAsync(Run(feedId, 2));
		await _testee.StartRunAsync(Run(feedId, 3));

		var runs = await _testee.GetRunsAsync(RunQuery.Take(2) with { MustInclude = Some.Of(RunId.From(Guid.NewGuid())) });

		await Assert.That(runs.Select(run => run.StartedAt)).IsEquivalentTo([Day(3), Day(2)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetRunsAsync_WhenTheRunToIncludeBelongsToAnotherFeed_ReturnsThePlainLimit()
	{
		var comedy = await SeedFeedAsync();
		var horror = await SeedFeedAsync();
		var elsewhere = Run(horror, 1);

		await _testee.StartRunAsync(elsewhere);
		await _testee.StartRunAsync(Run(comedy, 2));
		await _testee.StartRunAsync(Run(comedy, 3));
		await _testee.StartRunAsync(Run(comedy, 4));

		var runs = await _testee.GetRunsAsync(RunQuery.Take(2) with { Feed = Some.Of(comedy), MustInclude = Some.Of(elsewhere.Id) });

		await Assert.That(runs.Select(run => run.StartedAt)).IsEquivalentTo([Day(4), Day(3)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetRunsAsync_ForAnotherUsersRuns_NeverReturnsThemEvenWhenAskedToIncludeOne()
	{
		var theirs = Run(await SeedFeedAsync(), 1);
		await _testee.StartRunAsync(theirs);
		await fixture.SignInAs("Member");
		var mine = await SeedFeedAsync();
		await _testee.StartRunAsync(Run(mine, 2));
		await _testee.StartRunAsync(Run(mine, 3));
		await _testee.StartRunAsync(Run(mine, 4));

		var runs = await _testee.GetRunsAsync(RunQuery.Take(1) with { MustInclude = Some.Of(theirs.Id) });

		await Assert.That(runs.Select(run => run.StartedAt)).IsEquivalentTo([Day(4)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetRunTotalsAsync_SeparatesFeedsWithHistoryFromOnesThatNeverRan()
	{
		var ran = await SeedFeedAsync();
		var neverRan = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(ran, 1));

		var everRan = await _testee.GetRunTotalsAsync();

		await Assert.That(everRan).ContainsKey(ran);
		await Assert.That(everRan).DoesNotContainKey(neverRan);
	}

	private static FeedRunItem Item(MediaId mediaId, FeedRunItemState state) => new() { MediaId = mediaId, State = state };

	private static AbsenceChanges Missed(params MediaId[] missed) => new(missed.ToHashSet());

	private async Task CompleteAsync(FeedId feedId, int day, Optional<AbsenceChanges> absences)
	{
		var run = Run(feedId, day);
		await _testee.StartRunAsync(run);
		await _testee.CompleteRunAsync(run, absences);
	}

	private static DateTimeOffset Day(int day) => new(2026, 03, day, 4, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task GetRunTotalsAsync_ForAFeedWithRuns_TotalsTheCountsAndDatesTheFirstRun()
	{
		var feedId = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(feedId, 1) with { Counts = Counts(added: 3, removed: 1) });
		await _testee.StartRunAsync(Run(feedId, 2) with { Counts = Counts(added: 2, removed: 0) });

		var history = (await _testee.GetRunTotalsAsync())[feedId].History;

		await Assert.That(history.TotalRuns).IsEqualTo(2);
		await Assert.That(history.TotalAdded).IsEqualTo(5);
		await Assert.That(history.TotalRemoved).IsEqualTo(1);
		await Assert.That(history.FirstRunAt).IsSome().And.IsEqualTo(Day(1));
	}

	private static FeedRunCounts Counts(int added, int removed) =>
		new()
		{
			Total = added + removed,
			Added = added,
			Kept = 0,
			Bypassed = 0,
			Filtered = 0,
			Skipped = 0,
			UnmatchedNotFound = 0,
			UnmatchedCouldNotLookUp = 0,
			UnmatchedConflict = 0,
			Removed = removed,
			Known = 0,
			NotFoundEarlier = 0,
		};

	private static FeedRun Run(FeedId feedId, int day) =>
		new()
		{
			Id = RunId.From(Guid.NewGuid()),
			FeedId = feedId,
			StartedAt = Day(day),
			Duration = TimeSpan.FromSeconds(4),
			Strategy = new FeedStrategy(new Mirror(Window)),
			Trigger = new FeedRunTrigger(new Scheduled("0 0 4 * * *")),
			Conclusiveness = Conclusiveness.Conclusive,
			Counts = new FeedRunCounts
			{
				Total = 0,
				Added = 0,
				Kept = 0,
				Bypassed = 0,
				Filtered = 0,
				Skipped = 0,
				UnmatchedNotFound = 0,
				UnmatchedCouldNotLookUp = 0,
				UnmatchedConflict = 0,
				Removed = 0,
				Known = 0,
				NotFoundEarlier = 0,
			},
			Items = [],
		};

	private async Task<FeedId> SeedFeedAsync()
	{
		var entity = new FeedRow
		{
			Name = "Comedy",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Mirror(Window)),
			Identifier = "hdlists/comedy",
		};

		fixture.Context.Feeds.Add(entity);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return FeedId.From(entity.Id);
	}
}
