using Contracts.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Processing;
using Domain.Providers;
using Domain.Watchlist.Sync;
using TUnit.Assertions.Enums;
using UI.Features.Home.Components;

namespace UI.Tests.Home;

public class UpNextViewTests
{
	private static readonly FeedId Docs = FeedId.From(new Guid("00000000-0000-0000-0000-0000000000d0"));
	private static readonly FeedId Anime = FeedId.From(new Guid("00000000-0000-0000-0000-0000000000d1"));

	private static readonly Dictionary<FeedId, string> Names = new() { [Docs] = "hdlists/documentaries", [Anime] = "hdlists/anime" };
	private static readonly DateTimeOffset Now = new(2026, 9, 27, 14, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task InTheQueue_LabelsTheRunningJobAndNumbersTheRestByTheirPlaceInLine()
	{
		var queue = Snapshot(Job(Anime, JobState.Running), Job(Docs, JobState.Queued));

		var rows = UpNextView.InTheQueue(queue, Names, "Plex");

		await Assert.That(rows[0].Kind).IsEqualTo(UpNextKind.Running);
		await Assert.That(rows[0].When).IsEqualTo("running");
		await Assert.That(rows[1].When).IsEqualTo("2nd in line");
	}

	[Test]
	public async Task InTheQueue_NamesASyncJobAfterTheWatchlistProvider()
	{
		var queue = Snapshot(new PendingJob(JobId.From(Guid.NewGuid()), new SyncWatchlist(new ManualTrigger()), JobState.Queued, default, 0));

		var rows = UpNextView.InTheQueue(queue, Names, "Plex");

		await Assert.That(rows[0].Name).IsEqualTo("Sync to Plex");
	}

	[Test]
	public async Task InTheQueue_LeavesOutJobsThatAreNeitherRunningNorQueued()
	{
		var queue = Snapshot(Job(Docs, JobState.Done), Job(Anime, JobState.Failed));

		await Assert.That(UpNextView.InTheQueue(queue, Names, "Plex")).IsEmpty();
	}

	[Test]
	public async Task InTheQueue_ForAFeedThatWasDeleted_StillNamesTheRow()
	{
		var queue = Snapshot(Job(FeedId.From(Guid.NewGuid()), JobState.Queued));

		await Assert.That(UpNextView.InTheQueue(queue, Names, "Plex")[0].Name).IsEqualTo("A deleted feed");
	}

	[Test]
	public async Task OnASchedule_OrdersByWhenTheyActuallyRunAndSkipsManualFeeds()
	{
		IReadOnlyList<Feed> feeds =
		[
			Feed(Docs, "docs", new Scheduled("0 0 4 * * *")),
			Feed(Anime, "manual one"),
			Feed(FeedId.From(Guid.NewGuid()), "hourly", new Scheduled("0 0 * * * *")),
		];

		var rows = UpNextView.OnASchedule(feeds, Now, TimeZoneInfo.Utc);

		await Assert.That(rows.Select(row => row.Name)).IsEquivalentTo(["hourly", "docs"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task OnASchedule_LeavesOutDisabledFeeds()
	{
		IReadOnlyList<Feed> feeds = [Feed(Docs, "docs", new Scheduled("0 0 4 * * *")) with { Enabled = false }];

		await Assert.That(UpNextView.OnASchedule(feeds, Now, TimeZoneInfo.Utc)).IsEmpty();
	}

	[Test]
	public async Task Ordinal_ReadsTheWayEnglishDoesPastTheObviousCases()
	{
		await Assert.That(UpNextView.Ordinal(1)).IsEqualTo("1st");
		await Assert.That(UpNextView.Ordinal(4)).IsEqualTo("4th");
		await Assert.That(UpNextView.Ordinal(11)).IsEqualTo("11th");
		await Assert.That(UpNextView.Ordinal(21)).IsEqualTo("21st");
		await Assert.That(UpNextView.Ordinal(22)).IsEqualTo("22nd");
	}

	private static QueueSnapshot Snapshot(params PendingJob[] jobs) => new(jobs, 0);

	private static PendingJob Job(FeedId feedId, JobState state) => new(JobId.From(Guid.NewGuid()), new RunFeed(feedId, new Manual()), state, default, 0);

	private static Feed Feed(FeedId id, string name, FeedRunTrigger? schedule = null) =>
		new()
		{
			Name = name,
			Id = id,
			Enabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new Append(),
			Identifier = name,
			CreatedAt = Now.AddYears(-1),
			Schedule = schedule ?? new Manual(),
		};
}
