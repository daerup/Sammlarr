using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class RunPlanTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 27, 14, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task Candidates_PutsAFeedThatIsNotDueForDaysInWorthRunning()
	{
		var monthly = Feed("monthly", new Scheduled("0 0 6 1 * *"));

		var candidates = RunPlan.Candidates([NeverRan(monthly)], Now, TimeZoneInfo.Utc);

		await Assert.That(candidates[0].Group).IsEqualTo(RunGroup.WorthRunning);
		await Assert.That(candidates[0].Flag).IsEqualTo(RunFlag.NotDue);
	}

	[Test]
	public async Task Candidates_PutsAFeedRunningWithinTheDayInSoonAnyway()
	{
		var hourly = Feed("hourly", new Scheduled("0 0 * * * *"));

		var candidates = RunPlan.Candidates([NeverRan(hourly)], Now, TimeZoneInfo.Utc);

		await Assert.That(candidates[0].Group).IsEqualTo(RunGroup.SoonAnyway);
		await Assert.That(candidates[0].Flag).IsNull();
	}

	[Test]
	public async Task Candidates_PullsAFeedWhoseLastRunWasInconclusiveBackToWorthRunning()
	{
		var hourly = Feed("hourly", new Scheduled("0 0 * * * *"));

		var candidates = RunPlan.Candidates([Ran(hourly, Conclusiveness.NoMatches, 12)], Now, TimeZoneInfo.Utc);

		await Assert.That(candidates[0].Group).IsEqualTo(RunGroup.WorthRunning);
		await Assert.That(candidates[0].Flag).IsEqualTo(RunFlag.Inconclusive);
	}

	[Test]
	public async Task Candidates_FlagsAFeedWhoseSourceFailedLastTimeAsFailedRatherThanInconclusive()
	{
		var hourly = Feed("hourly", new Scheduled("0 0 * * * *"));

		var candidates = RunPlan.Candidates([Ran(hourly, Conclusiveness.StillLoading, 0)], Now, TimeZoneInfo.Utc);

		await Assert.That(candidates[0].Group).IsEqualTo(RunGroup.WorthRunning);
		await Assert.That(candidates[0].Flag).IsEqualTo(RunFlag.Failed);
	}

	[Test]
	public async Task Candidates_KeepsManualFeedsInTheirOwnGroupEvenThoughTheyAreNeverDue()
	{
		var manual = Feed("by hand");

		var candidates = RunPlan.Candidates([NeverRan(manual)], Now, TimeZoneInfo.Utc);

		await Assert.That(candidates[0].Group).IsEqualTo(RunGroup.ManualOnly);
		await Assert.That(candidates[0].NextRun).IsNull();
	}

	[Test]
	public async Task Candidates_ShowsADisabledFeedButNeverPreSelectsIt()
	{
		var off = Feed("off", new Scheduled("0 0 * * * *"), enabled: false);

		var candidates = RunPlan.Candidates([NeverRan(off)], Now, TimeZoneInfo.Utc);

		await Assert.That(candidates).HasSingleItem();
		await Assert.That(candidates[0].Enabled).IsFalse();
		await Assert.That(candidates[0].Flag).IsEqualTo(RunFlag.Disabled);
		await Assert.That(RunPlan.DefaultSelection(candidates)).IsEmpty();
	}

	[Test]
	public async Task Candidates_ForADisabledScheduledFeed_ShowsNoNextRunBecauseItWillNotRun()
	{
		var off = Feed("off", new Scheduled("0 0 * * * *"), enabled: false);

		var candidates = RunPlan.Candidates([NeverRan(off)], Now, TimeZoneInfo.Utc);

		await Assert.That(candidates[0].NextRun).IsNull();
	}

	[Test]
	public async Task Candidates_OrdersWorthRunningFirstThenSoonThenManual()
	{
		IReadOnlyList<Feed> feeds = [Feed("by hand"), Feed("hourly", new Scheduled("0 0 * * * *")), Feed("monthly", new Scheduled("0 0 6 1 * *"))];

		var candidates = RunPlan.Candidates([.. feeds.Select(NeverRan)], Now, TimeZoneInfo.Utc);

		await Assert.That(candidates.Select(candidate => candidate.Name)).IsEquivalentTo(["monthly", "hourly", "by hand"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task DefaultSelection_TicksOnlyTheFeedsWorthRunning()
	{
		IReadOnlyList<Feed> feeds = [Feed("by hand"), Feed("hourly", new Scheduled("0 0 * * * *")), Feed("monthly", new Scheduled("0 0 6 1 * *"))];
		var candidates = RunPlan.Candidates([.. feeds.Select(NeverRan)], Now, TimeZoneInfo.Utc);

		var selected = RunPlan.DefaultSelection(candidates);

		await Assert.That(selected).HasSingleItem();
		await Assert.That(selected.Contains(feeds[2].Id)).IsTrue();
	}

	[Test]
	public async Task Candidates_ForAFeedThatNeverRan_SaysSoRatherThanShowingAZeroDelta()
	{
		var candidates = RunPlan.Candidates([NeverRan(Feed("fresh", new Scheduled("0 0 * * * *")))], Now, TimeZoneInfo.Utc);

		await Assert.That(candidates[0].LastRun.EverRan).IsFalse();
		await Assert.That(candidates[0].LastRun.Changed).IsFalse();
	}

	private static FeedOverview NeverRan(Feed feed) => FeedOverview.Of(feed, null, 0);

	private static FeedOverview Ran(Feed feed, Conclusiveness outcome, int fetched) =>
		FeedOverview.Of(
			feed,
			new FeedRunTotals(
				new FeedRun
				{
					Id = Guid.NewGuid(),
					FeedId = feed.Id,
					StartedAt = Now.AddHours(-1),
					Duration = TimeSpan.FromSeconds(2),
					Strategy = new Append(),
					Trigger = new Manual(),
					Conclusiveness = outcome,
					Counts = FeedRunCounts.None with { Total = fetched },
					Items = [],
				},
				FeedRunHistory.None
			),
			0
		);

	private static Feed Feed(string name, FeedRunTrigger? schedule = null, bool enabled = true) =>
		new()
		{
			Name = name,
			Id = FeedId.From(Guid.NewGuid()),
			Enabled = enabled,
			Provider = FeedProvider.Mdblist,
			Strategy = new Append(),
			Identifier = name,
			CreatedAt = Now.AddYears(-1),
			Schedule = schedule ?? new Manual(),
		};
}
