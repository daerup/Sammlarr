using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using TUnit.Assertions.Enums;
using UI.Features.Home.Components;

namespace UI.Tests.Home;

public class ActivityLogTests
{
	private static readonly FeedId Docs = FeedId.From(new Guid("00000000-0000-0000-0000-0000000000d0"));
	private static readonly FeedId Trending = FeedId.From(new Guid("00000000-0000-0000-0000-0000000000d1"));

	private static readonly Dictionary<FeedId, string> Names = new()
	{
		[Docs] = "hdlists/latest-hd-documentary-movies",
		[Trending] = "TMDB · Trending TV this week",
	};

	private static readonly DateTimeOffset Today = new(2026, 9, 27, 14, 32, 0, TimeSpan.Zero);

	[Test]
	public async Task Days_CountsQuietRunsInTheDayTotalWithoutGivingThemAnEntry()
	{
		var runs = new[] { Run(Docs, Today, added: 2), Run(Trending, Today.AddHours(-1)), Run(Trending, Today.AddHours(-2)) };

		var days = ActivityLog.Days(runs, Names, TimeZoneInfo.Utc);

		await Assert.That(days).HasSingleItem();
		await Assert.That(days[0].ChangedRuns).IsEqualTo(1);
		await Assert.That(days[0].RunCount).IsEqualTo(3);
	}

	[Test]
	public async Task Days_FlattensEveryTitleAddedThatDayAcrossFeeds()
	{
		var runs = new[] { Run(Docs, Today, added: 2), Run(Trending, Today.AddHours(-1), added: 3) };

		var days = ActivityLog.Days(runs, Names, TimeZoneInfo.Utc);

		await Assert.That(days[0].Added).Count().IsEqualTo(5);
		await Assert.That(days[0].ChangedRuns).IsEqualTo(2);
	}

	[Test]
	public async Task Days_WhenTwoFeedsAddedTheSameTitleThatDay_CountsAndShowsItOnce()
	{
		var shared = MediaId.From(Guid.NewGuid());
		var runs = new[] { Run(Docs, Today, addedIds: [shared]), Run(Trending, Today.AddHours(-1), addedIds: [shared]) };

		var days = ActivityLog.Days(runs, Names, TimeZoneInfo.Utc);

		await Assert.That(days[0].Added).HasSingleItem();
		await Assert.That(days[0].ChangedRuns).IsEqualTo(2);
	}

	[Test]
	public async Task Days_SplitsRemovalsOutFromAdditions()
	{
		var runs = new[] { Run(Docs, Today, added: 2, removed: 1) };

		var days = ActivityLog.Days(runs, Names, TimeZoneInfo.Utc);

		await Assert.That(days[0].Added).Count().IsEqualTo(2);
		await Assert.That(days[0].Removed).HasSingleItem();
	}

	[Test]
	public async Task Days_KeepsAnInconclusiveRunEvenThoughItChangedNothing()
	{
		var runs = new[] { Run(Docs, Today, conclusiveness: Conclusiveness.NoMatches) };

		var days = ActivityLog.Days(runs, Names, TimeZoneInfo.Utc);

		await Assert.That(days).HasSingleItem();
		await Assert.That(days[0].Inconclusive).HasSingleItem();
		await Assert.That(days[0].ChangedRuns).IsEqualTo(0);
	}

	[Test]
	public async Task Days_DropsADayWhereEveryRunWasQuiet()
	{
		var runs = new[] { Run(Docs, Today, added: 1), Run(Docs, Today.AddDays(-1)), Run(Docs, Today.AddDays(-1)) };

		var days = ActivityLog.Days(runs, Names, TimeZoneInfo.Utc);

		await Assert.That(days).HasSingleItem();
		await Assert.That(days[0].Date).IsEqualTo(new DateOnly(2026, 9, 27));
	}

	[Test]
	public async Task Days_OrdersNewestFirstAcrossDaysAndWithinADay()
	{
		var runs = new[] { Run(Trending, Today.AddDays(-1), added: 1), Run(Docs, Today.AddHours(-3), added: 1), Run(Trending, Today, added: 1) };

		var days = ActivityLog.Days(runs, Names, TimeZoneInfo.Utc);

		await Assert.That(days.Select(day => day.Date)).IsEquivalentTo([new DateOnly(2026, 9, 27), new DateOnly(2026, 9, 26)], CollectionOrdering.Matching);
		await Assert.That(days[0].Feeds[0].Name).IsEqualTo("TMDB · Trending TV this week");
	}

	[Test]
	public async Task Days_ForARunWhoseFeedWasDeleted_StillNamesTheRow()
	{
		var runs = new[] { Run(FeedId.From(Guid.NewGuid()), Today, added: 1) };

		var days = ActivityLog.Days(runs, Names, TimeZoneInfo.Utc);

		await Assert.That(days[0].Feeds[0].Name).IsEqualTo("A deleted feed");
	}

	[Test]
	public async Task Outcome_ForEachInconclusiveReason_ExplainsWhatHappened()
	{
		var noItems = new ActivityRun(Run(Docs, Today, conclusiveness: Conclusiveness.NoItems), "feed");
		var noneAdmitted = new ActivityRun(Run(Docs, Today, total: 40, conclusiveness: Conclusiveness.NoneAdmitted), "feed");

		await Assert.That(noItems.Outcome).IsEqualTo("the list came back empty");
		await Assert.That(noneAdmitted.Outcome).IsEqualTo("all 40 titles were filtered out");
	}

	[Test]
	public async Task Reason_ForAFailedRun_QuotesTheSourceAndFallsBackWhenNothingWasRecorded()
	{
		var broken = new ActivityRun(
			Run(Docs, Today, conclusiveness: Conclusiveness.StillLoading) with
			{
				SourceFailure = "No MDBList API key is configured.",
			},
			"feed"
		);
		var empty = new ActivityRun(Run(Docs, Today, conclusiveness: Conclusiveness.NoItems), "feed");
		var unrecorded = new ActivityRun(Run(Docs, Today, conclusiveness: Conclusiveness.StillLoading), "feed");

		await Assert.That(broken.Reason).IsEqualTo("No MDBList API key is configured.");
		await Assert.That(empty.Reason).IsEqualTo("The list came back empty.");
		await Assert.That(unrecorded.Reason).IsEqualTo("The reason wasn't recorded.");
	}

	[Test]
	public async Task Days_SummarisesEachFeedsWholeDayWithItsRunCount()
	{
		var runs = new[]
		{
			Run(Docs, Today, added: 5, removed: 5),
			Run(Docs, Today.AddHours(-1), added: 5, removed: 5),
			Run(Trending, Today.AddHours(-2), added: 2),
		};

		var days = ActivityLog.Days(runs, Names, TimeZoneInfo.Utc);

		await Assert.That(days[0].Feeds).Count().IsEqualTo(2);
		await Assert.That(days[0].Feeds[0].Added).IsEqualTo(10);
		await Assert.That(days[0].Feeds[0].Removed).IsEqualTo(10);
		await Assert.That(days[0].Feeds[0].Runs).IsEqualTo(2);
		await Assert.That(days[0].Feeds[1].Runs).IsEqualTo(1);
	}

	[Test]
	public async Task Days_PointsEachFeedAtItsLatestRunOfThatDayNotItsLatestEver()
	{
		var earlier = Run(Docs, Today.AddHours(-5), added: 1);
		var latest = Run(Docs, Today.AddHours(-1), added: 1);
		var tomorrowish = Run(Docs, Today.AddDays(1), added: 1);

		var days = ActivityLog.Days([tomorrowish, latest, earlier], Names, TimeZoneInfo.Utc);
		var today = days.Single(day => day.Date == new DateOnly(2026, 9, 27));

		await Assert.That(today.Feeds[0].LatestRunId).IsEqualTo(latest.Id);
	}

	private static FeedRun Run(
		FeedId feedId,
		DateTimeOffset startedAt,
		int added = 0,
		int removed = 0,
		int total = 0,
		Conclusiveness conclusiveness = Conclusiveness.Conclusive,
		IReadOnlyList<MediaId>? addedIds = null
	) =>
		new()
		{
			Id = RunId.From(Guid.NewGuid()),
			FeedId = feedId,
			StartedAt = startedAt,
			Duration = TimeSpan.FromSeconds(4),
			Strategy = new Mirror(),
			Trigger = new Scheduled("0 0 4 * * *"),
			Conclusiveness = conclusiveness,
			Counts = FeedRunCounts.None with { Total = total, Added = addedIds?.Count ?? added, Removed = removed },
			Items =
			[
				.. (addedIds ?? [.. Enumerable.Range(0, added).Select(_ => MediaId.From(Guid.NewGuid()))]).Select(id => new FeedRunItem
				{
					MediaId = id,
					State = FeedRunItemState.Added,
				}),
				.. Enumerable.Range(0, removed).Select(_ => Item(FeedRunItemState.Removed)),
			],
		};

	private static FeedRunItem Item(FeedRunItemState state) => new() { MediaId = MediaId.From(Guid.NewGuid()), State = state };
}
