using Contracts.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Providers;
using UI.Features.Feeds.Components;
using UrlGuid = global::Shared.UrlGuid;

namespace UI.Features.Home.Components;

public enum UpNextKind
{
	Running,
	Queued,
	Waiting,
	Scheduled,
}

public sealed record UpNextRow(
	string Name,
	string Detail,
	string When,
	UpNextKind Kind,
	string? Href,
	MediaProvider? Brand = null,
	bool Deleted = false,
	DateTimeOffset? RunsAt = null,
	JobId? Job = null
)
{
	public bool CanCancel => Job is not null && Kind is UpNextKind.Queued or UpNextKind.Waiting;
}

internal static class UpNextView
{
	internal static IReadOnlyList<UpNextRow> InTheQueue(
		QueueSnapshot queue,
		IReadOnlyDictionary<FeedId, string> feedNames,
		IReadOnlyDictionary<FeedId, MediaProvider> feedBrands,
		MediaProvider watchlistProvider
	) =>
		[
			.. queue
				.Jobs.Where(job => job.State is JobState.Waiting or JobState.Queued or JobState.Running)
				.Select(job => Row(job, feedNames, feedBrands, watchlistProvider)),
		];

	internal static IReadOnlySet<FeedId> FeedsAlreadyPending(QueueSnapshot queue) =>
		queue
			.Jobs.Where(job => job.State is JobState.Waiting or JobState.Queued or JobState.Running)
			.Select(job => job.Request)
			.OfType<RunFeed>()
			.Select(feed => feed.Id)
			.ToHashSet();

	internal static IReadOnlyList<UpNextRow> OnASchedule(IReadOnlyList<Feed> feeds, DateTimeOffset from, TimeZoneInfo zone) =>
		[
			.. feeds
				.Where(feed => feed.Enabled)
				.Select(feed => (feed, next: feed.Schedule is Scheduled scheduled ? scheduled.NextRun(from, zone) : null))
				.Where(entry => entry.next is not null)
				.OrderBy(entry => entry.next)
				.Select(entry => new UpNextRow(
					entry.feed.Name,
					ScheduleText.Describe(entry.feed.Schedule),
					ScheduleText.Away(entry.next!.Value, from),
					UpNextKind.Scheduled,
					EditHref(entry.feed.Id),
					entry.feed.Provider.AsMediaProvider()
				)),
		];

	private static UpNextRow Row(
		PendingJob job,
		IReadOnlyDictionary<FeedId, string> feedNames,
		IReadOnlyDictionary<FeedId, MediaProvider> feedBrands,
		MediaProvider watchlistProvider
	)
	{
		var name = Name(job.Request, feedNames);
		var href = Href(job.Request, feedNames);
		var brand = Brand(job.Request, feedBrands, watchlistProvider);
		var deleted = Deleted(job.Request, feedNames);
		return job.State is JobState.Running ? new UpNextRow(name, "running now", "running", UpNextKind.Running, href, brand, deleted, Job: job.Id)
			: job.State is JobState.Waiting ? new UpNextRow(name, Waiting(job.Request), string.Empty, UpNextKind.Waiting, href, brand, deleted, job.RunAfter, job.Id)
			: new UpNextRow(name, "queued", string.Empty, UpNextKind.Queued, href, brand, deleted, Job: job.Id);
	}

	private static MediaProvider? Brand(IJobRequest request, IReadOnlyDictionary<FeedId, MediaProvider> feedBrands, MediaProvider watchlistProvider) =>
		request switch
		{
			RunFeed feed => feedBrands.TryGetValue(feed.Id, out var provider) ? provider : null,
			SyncWatchlist => watchlistProvider,
			_ => null,
		};

	private static string Waiting(IJobRequest request) => request is SyncWatchlist ? "waiting for grace period" : "waiting";

	private static bool Deleted(IJobRequest request, IReadOnlyDictionary<FeedId, string> feedNames) => request is RunFeed feed && !feedNames.ContainsKey(feed.Id);

	private static string? Href(IJobRequest request, IReadOnlyDictionary<FeedId, string> feedNames) =>
		request is RunFeed feed && feedNames.ContainsKey(feed.Id) ? EditHref(feed.Id) : null;

	private static string EditHref(FeedId feedId) => $"/feeds/edit/{UrlGuid.Encode(feedId.Value)}";

	private static string Name(IJobRequest request, IReadOnlyDictionary<FeedId, string> feedNames) =>
		request switch
		{
			RunFeed feed => FeedLabel.Of(feed.Id, feedNames),
			SyncWatchlist => "Sync Watchlist",
			_ => request.Kind.GetDescription(),
		};
}
