using Contracts.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Processing;
using Domain.Providers;
using Domain.Wantlist.Sync;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Home.Components;
using UrlGuid = global::Shared.UrlGuid;

namespace UI.Tests.Home;

public class UpNextViewTests
{
	private static readonly FeedId Docs = FeedId.From(new Guid("00000000-0000-0000-0000-0000000000d0"));
	private static readonly FeedId Anime = FeedId.From(new Guid("00000000-0000-0000-0000-0000000000d1"));

	private static readonly Dictionary<FeedId, string> Names = new() { [Docs] = "hdlists/documentaries", [Anime] = "hdlists/anime" };

	private static readonly Dictionary<FeedId, MediaProvider> Brands = new() { [Docs] = MediaProvider.Mdblist, [Anime] = MediaProvider.Tmdb };
	private static readonly DateTimeOffset Now = new(2026, 9, 27, 14, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task InTheQueue_MarksTheRunningJobAndLeavesQueuedOnesWithoutATimeOfTheirOwn()
	{
		var queue = Snapshot(Job(Anime, JobState.Running), Job(Docs, JobState.Queued));

		var rows = UpNextView.InTheQueue(queue, Names, Brands, MediaProvider.Plex);

		await Assert.That(rows[0].Kind).IsEqualTo(UpNextKind.Running);
		await Assert.That(rows[0].When).IsEqualTo("running");
		await Assert.That(rows[1].Kind).IsEqualTo(UpNextKind.Queued);
		await Assert.That(rows[1].When).IsEmpty();
		await Assert.That(rows[0].RunsAt).IsNone();
		await Assert.That(rows[1].RunsAt).IsNone();
	}

	[Test]
	public async Task InTheQueue_ForAFeedJob_CarriesTheFeedsOwnProviderSoTheRowShowsItsBrand()
	{
		var queue = Snapshot(Job(Anime, JobState.Queued));

		var row = UpNextView.InTheQueue(queue, Names, Brands, MediaProvider.Plex)[0];

		await Assert.That(row.Brand).IsEqualTo(MediaProvider.Tmdb);
	}

	[Test]
	public async Task InTheQueue_ForASyncJob_CarriesTheSinkSoTheRowShowsItsBrand()
	{
		var queue = Snapshot(new PendingJob(JobId.From(Guid.NewGuid()), new SyncWantlist(new ManualTrigger()), JobState.Queued, default, 0));

		var row = UpNextView.InTheQueue(queue, Names, Brands, MediaProvider.Plex)[0];

		await Assert.That(row.Brand).IsEqualTo(MediaProvider.Plex);
	}

	[Test]
	public async Task InTheQueue_ForAFeedThatWasDeleted_HasNoBrandToShow()
	{
		var queue = Snapshot(Job(FeedId.From(Guid.NewGuid()), JobState.Queued));

		await Assert.That(UpNextView.InTheQueue(queue, Names, Brands, MediaProvider.Plex)[0].Brand).IsNull();
	}

	[Test]
	public async Task InTheQueue_NamesASyncJobSyncWantlist()
	{
		var queue = Snapshot(new PendingJob(JobId.From(Guid.NewGuid()), new SyncWantlist(new ManualTrigger()), JobState.Queued, default, 0));

		var rows = UpNextView.InTheQueue(queue, Names, Brands, MediaProvider.Plex);

		await Assert.That(rows[0].Name).IsEqualTo("Sync Wantlist");
	}

	[Test]
	public async Task InTheQueue_ForASyncStillInsideItsGrace_SaysItWaitsForTheGraceAndCarriesWhenItRuns()
	{
		var queue = Snapshot(new PendingJob(JobId.From(Guid.NewGuid()), new SyncWantlist(new ScheduledFeedTrigger()), JobState.Waiting, Now.AddMinutes(2), 0));

		var row = UpNextView.InTheQueue(queue, Names, Brands, MediaProvider.Plex)[0];

		await Assert.That(row.Kind).IsEqualTo(UpNextKind.Waiting);
		await Assert.That(row.Detail).IsEqualTo("waiting for grace period");
		await Assert.That(row.RunsAt).IsSome().And.IsEqualTo(Now.AddMinutes(2));
	}

	[Test]
	public async Task InTheQueue_LeavesOutJobsThatHaveAlreadySettled()
	{
		var queue = Snapshot(Job(Docs, JobState.Done), Job(Anime, JobState.Failed));

		await Assert.That(UpNextView.InTheQueue(queue, Names, Brands, MediaProvider.Plex)).IsEmpty();
	}

	[Test]
	public async Task InTheQueue_ForAFeedThatWasDeleted_NamesTheRowMarksItDeletedAndLinksNowhere()
	{
		var queue = Snapshot(Job(FeedId.From(Guid.NewGuid()), JobState.Queued));

		var row = UpNextView.InTheQueue(queue, Names, Brands, MediaProvider.Plex)[0];

		await Assert.That(row.Name).IsEqualTo("A deleted feed");
		await Assert.That(row.Href).IsNull();
		await Assert.That(row.Deleted).IsTrue();
	}

	[Test]
	public async Task InTheQueue_ForALiveFeedAndASync_LinksOnlyTheFeedAndMarksNeitherDeleted()
	{
		var queue = Snapshot(
			Job(Docs, JobState.Running),
			new PendingJob(JobId.From(Guid.NewGuid()), new SyncWantlist(new ManualTrigger()), JobState.Queued, default, 0)
		);

		var rows = UpNextView.InTheQueue(queue, Names, Brands, MediaProvider.Plex);

		await Assert.That(rows[0].Href).IsEqualTo($"/feeds/edit/{UrlGuid.Encode(Docs.Value)}");
		await Assert.That(rows[1].Href).IsNull();
		await Assert.That(rows.Select(row => row.Deleted)).IsEquivalentTo([false, false]);
	}

	[Test]
	public async Task OnASchedule_LinksEachFeedToItsEditor()
	{
		IReadOnlyList<Feed> feeds = [Feed(Docs, "docs", new Scheduled("0 0 4 * * *"))];

		var rows = UpNextView.OnASchedule(feeds, Now, TimeZoneInfo.Utc);

		await Assert.That(rows[0].Href).IsEqualTo($"/feeds/edit/{UrlGuid.Encode(Docs.Value)}");
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
	public async Task OnASchedule_GivesTheRowsNoRunTimeOfTheirOwn()
	{
		IReadOnlyList<Feed> feeds = [Feed(Docs, "docs", new Scheduled("0 0 4 * * *"))];

		var rows = UpNextView.OnASchedule(feeds, Now, TimeZoneInfo.Utc);

		await Assert.That(rows.Single().RunsAt).IsNone();
	}

	[Test]
	public async Task OnASchedule_LeavesOutDisabledFeeds()
	{
		IReadOnlyList<Feed> feeds = [Feed(Docs, "docs", new Scheduled("0 0 4 * * *")) with { Enabled = false }];

		await Assert.That(UpNextView.OnASchedule(feeds, Now, TimeZoneInfo.Utc)).IsEmpty();
	}

	[Test]
	public async Task InTheQueue_LetsOnlyTheJobsThatHaveNotStartedBeCancelled()
	{
		var queue = Snapshot(
			Job(Anime, JobState.Running),
			Job(Docs, JobState.Queued),
			new PendingJob(JobId.From(Guid.NewGuid()), new SyncWantlist(new ScheduledFeedTrigger()), JobState.Waiting, Now.AddMinutes(2), 0)
		);

		var rows = UpNextView.InTheQueue(queue, Names, Brands, MediaProvider.Plex);

		await Assert.That(rows.Select(row => row.CanCancel)).IsEquivalentTo([false, true, true], CollectionOrdering.Matching);
		await Assert.That(rows.Select(row => row.Job)).IsEquivalentTo(queue.Jobs.Select(job => (JobId?)job.Id), CollectionOrdering.Matching);
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
