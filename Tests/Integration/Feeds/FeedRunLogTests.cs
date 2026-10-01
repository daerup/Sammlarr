using Contracts.Feed;
using DB.Data.Feed;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;
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
	private static readonly IReadOnlySet<MediaId> Owned = new HashSet<MediaId> { Missing, Held };

	private IFeedRepository _testee = null!;

	[Before(Test)]
	public void Setup() => _testee = new FeedRepository(fixture.ContextFactory);

	[Test]
	public async Task GetAbsencesAsync_WhenAnItemIsMissingAcrossRuns_CountsTheConsecutiveRuns()
	{
		var feedId = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(feedId, kept: [Held], 1));
		await _testee.StartRunAsync(Run(feedId, kept: [Held], 2));

		var absences = await _testee.GetAbsencesAsync(feedId, Owned, Window);

		await Assert.That(absences[Missing]).IsEqualTo(2);
		await Assert.That(absences).DoesNotContainKey(Held);
	}

	[Test]
	public async Task GetAbsencesAsync_WhenAMissingItemComesBack_RestartsTheCountFromScratch()
	{
		var feedId = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(feedId, kept: [Held], 1));
		await _testee.StartRunAsync(Run(feedId, kept: [Held], 2));
		await _testee.StartRunAsync(Run(feedId, kept: [Held, Missing], 3));
		await _testee.StartRunAsync(Run(feedId, kept: [Held], 4));

		var absences = await _testee.GetAbsencesAsync(feedId, Owned, Window);

		await Assert.That(absences[Missing]).IsEqualTo(1);
	}

	[Test]
	public async Task GetAbsencesAsync_WhenARunWasInconclusive_SkipsItRatherThanCountingIt()
	{
		var feedId = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(feedId, kept: [Held], 1));
		await _testee.StartRunAsync(Run(feedId, kept: [Held, Missing], 2) with { Conclusiveness = Conclusiveness.NoItems });
		await _testee.StartRunAsync(Run(feedId, kept: [Held], 3));

		var absences = await _testee.GetAbsencesAsync(feedId, Owned, Window);

		await Assert.That(absences[Missing]).IsEqualTo(2);
	}

	[Test]
	public async Task GetAbsencesAsync_WhenAnItemWasAddedThisRun_ReportsNoAbsence()
	{
		var feedId = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(feedId, kept: [Held], 1));
		await _testee.StartRunAsync(Run(feedId, kept: [Held], 2) with { Items = [Item(Held, FeedRunItemState.Kept), Item(Missing, FeedRunItemState.Added)] });

		var absences = await _testee.GetAbsencesAsync(feedId, Owned, Window);

		await Assert.That(absences).DoesNotContainKey(Missing);
	}

	[Test]
	public async Task RecordAsync_ForEveryRun_StoresTheCountsAndTheItemStates()
	{
		var feedId = await SeedFeedAsync();

		await _testee.StartRunAsync(
			Run(feedId, kept: [Held], 1) with
			{
				Counts = new FeedRunCounts
				{
					Total = 182,
					Added = 1,
					Kept = 178,
					Bypassed = 2,
					Filtered = 12,
					Skipped = 4,
					Unmatched = 2,
					Removed = 1,
				},
				Items = [Item(Held, FeedRunItemState.Kept), Item(Missing, FeedRunItemState.Removed)],
			}
		);

		var run = (await _testee.GetRunsAsync(RunQuery.Take(10) with { Feed = Some.Of(feedId) })).Single();

		await Assert.That(run.Counts.Kept).IsEqualTo(178);
		await Assert.That(run.Counts.Filtered).IsEqualTo(12);
		await Assert.That(run.Items.Single(item => item.State is FeedRunItemState.Removed).MediaId).IsEqualTo(Missing);
	}

	[Test]
	public async Task GetRunsAsync_ForOneFeed_ReturnsItsNewestRunsFirstUpToTheLimit()
	{
		var comedy = await SeedFeedAsync();
		var horror = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(comedy, kept: [Held], 1));
		await _testee.StartRunAsync(Run(comedy, kept: [Held], 2));
		await _testee.StartRunAsync(Run(comedy, kept: [Held], 3));
		await _testee.StartRunAsync(Run(horror, kept: [Held], 4));

		var runs = await _testee.GetRunsAsync(RunQuery.Take(2) with { Feed = Some.Of(comedy) });

		await Assert.That(runs.Select(run => run.StartedAt)).IsEquivalentTo([Day(3), Day(2)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetRunsAsync_SinceACutoff_ReturnsEveryFeedsRunsFromThenNewestFirst()
	{
		var comedy = await SeedFeedAsync();
		var horror = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(comedy, kept: [Held], 1));
		await _testee.StartRunAsync(Run(comedy, kept: [Held], 3));
		await _testee.StartRunAsync(Run(horror, kept: [Held], 2));

		var runs = await _testee.GetRunsAsync(RunQuery.Take(10) with { Since = Some.Of(Day(2)) });

		await Assert.That(runs.Select(run => run.FeedId)).IsEquivalentTo([comedy, horror], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetRunsAsync_ForACutoffInAnotherOffset_ComparesTheSameInstant()
	{
		var feedId = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(feedId, kept: [Held], 2));

		var runs = await _testee.GetRunsAsync(RunQuery.Take(10) with { Since = Some.Of(Day(2).ToOffset(TimeSpan.FromHours(2))) });

		await Assert.That(runs).HasSingleItem();
	}

	[Test]
	public async Task GetRunsAsync_WhenTheRunToIncludeIsOlderThanTheLimit_KeepsGoingUntilItIsInTheList()
	{
		var feedId = await SeedFeedAsync();
		var oldest = Run(feedId, kept: [Held], 1);

		await _testee.StartRunAsync(oldest);
		await _testee.StartRunAsync(Run(feedId, kept: [Held], 2));
		await _testee.StartRunAsync(Run(feedId, kept: [Held], 3));
		await _testee.StartRunAsync(Run(feedId, kept: [Held], 4));

		var runs = await _testee.GetRunsAsync(RunQuery.Take(2) with { Feed = Some.Of(feedId), MustInclude = Some.Of(oldest.Id) });

		await Assert.That(runs.Select(run => run.StartedAt)).IsEquivalentTo([Day(4), Day(3), Day(2), Day(1)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetRunsAsync_WhenTheRunToIncludeIsUnknown_ReturnsThePlainLimit()
	{
		var feedId = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(feedId, kept: [Held], 1));
		await _testee.StartRunAsync(Run(feedId, kept: [Held], 2));
		await _testee.StartRunAsync(Run(feedId, kept: [Held], 3));

		var runs = await _testee.GetRunsAsync(RunQuery.Take(2) with { MustInclude = Some.Of(RunId.From(Guid.NewGuid())) });

		await Assert.That(runs.Select(run => run.StartedAt)).IsEquivalentTo([Day(3), Day(2)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetRunsAsync_WhenTheRunToIncludeBelongsToAnotherFeed_ReturnsThePlainLimit()
	{
		var comedy = await SeedFeedAsync();
		var horror = await SeedFeedAsync();
		var elsewhere = Run(horror, kept: [Held], 1);

		await _testee.StartRunAsync(elsewhere);
		await _testee.StartRunAsync(Run(comedy, kept: [Held], 2));
		await _testee.StartRunAsync(Run(comedy, kept: [Held], 3));
		await _testee.StartRunAsync(Run(comedy, kept: [Held], 4));

		var runs = await _testee.GetRunsAsync(RunQuery.Take(2) with { Feed = Some.Of(comedy), MustInclude = Some.Of(elsewhere.Id) });

		await Assert.That(runs.Select(run => run.StartedAt)).IsEquivalentTo([Day(4), Day(3)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetRunsAsync_ForAnotherUsersRuns_NeverReturnsThemEvenWhenAskedToIncludeOne()
	{
		var theirs = Run(await SeedFeedAsync(), kept: [Held], 1);
		await _testee.StartRunAsync(theirs);
		await fixture.SignInAs("Member");
		var mine = await SeedFeedAsync();
		await _testee.StartRunAsync(Run(mine, kept: [Held], 2));
		await _testee.StartRunAsync(Run(mine, kept: [Held], 3));
		await _testee.StartRunAsync(Run(mine, kept: [Held], 4));

		var runs = await _testee.GetRunsAsync(RunQuery.Take(1) with { MustInclude = Some.Of(theirs.Id) });

		await Assert.That(runs.Select(run => run.StartedAt)).IsEquivalentTo([Day(4)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetRunTotalsAsync_SeparatesFeedsWithHistoryFromOnesThatNeverRan()
	{
		var ran = await SeedFeedAsync();
		var neverRan = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(ran, kept: [Held], 1));

		var everRan = await _testee.GetRunTotalsAsync();

		await Assert.That(everRan).ContainsKey(ran);
		await Assert.That(everRan).DoesNotContainKey(neverRan);
	}

	private static FeedRunItem Item(MediaId mediaId, FeedRunItemState state) => new() { MediaId = mediaId, State = state };

	private static DateTimeOffset Day(int day) => new(2026, 03, day, 4, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task GetRunTotalsAsync_ForAFeedWithRuns_TotalsTheCountsAndDatesTheFirstRun()
	{
		var feedId = await SeedFeedAsync();

		await _testee.StartRunAsync(Run(feedId, kept: [Held], 1) with { Counts = Counts(added: 3, removed: 1) });
		await _testee.StartRunAsync(Run(feedId, kept: [Held], 2) with { Counts = Counts(added: 2, removed: 0) });

		var history = (await _testee.GetRunTotalsAsync())[feedId].History;

		await Assert.That(history.TotalRuns).IsEqualTo(2);
		await Assert.That(history.TotalAdded).IsEqualTo(5);
		await Assert.That(history.TotalRemoved).IsEqualTo(1);
		await Assert.That(history.FirstRunAt).IsEqualTo(Day(1));
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
			Unmatched = 0,
			Removed = removed,
		};

	private static FeedRun Run(FeedId feedId, IReadOnlyList<MediaId> kept, int day) =>
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
				Unmatched = 0,
				Removed = 0,
			},
			Items = [.. kept.Select(id => Item(id, FeedRunItemState.Kept))],
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
