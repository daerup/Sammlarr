using Contracts.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Providers;
using UI.Features.Feeds.Components;
using UrlGuid = Shared.UrlGuid;

namespace UI.Features.Home.Components;

public enum UpNextKind
{
	Running,
	Queued,
	Waiting,
	Scheduled,
}

public sealed record UpNextRow(string Name, string Detail, string When, UpNextKind Kind, string? Href, bool Deleted = false)
{
	public IReadOnlyList<MediaProvider> Brands { get; init; } = [];

	public Optional<DateTimeOffset> RunsAt { get; init; } = new None();

	public Optional<JobId> Job { get; init; } = new None();

	public bool CanCancel => Job.IsSome() && Kind is UpNextKind.Queued or UpNextKind.Waiting;
}

internal static class UpNextView
{
	internal static IReadOnlyList<UpNextRow> InTheQueue(
		QueueSnapshot queue,
		IReadOnlyDictionary<FeedId, string> feedNames,
		IReadOnlyDictionary<FeedId, MediaProvider> feedBrands,
		IReadOnlyList<MediaProvider> sinkProviders
	)
	{
		return
		[
			.. queue
				.Jobs.Where(job => job.State is JobState.Waiting or JobState.Queued or JobState.Running)
				.Select(job => Row(job, feedNames, feedBrands, sinkProviders)),
		];
	}

	internal static IReadOnlySet<FeedId> FeedsAlreadyPending(QueueSnapshot queue) =>
		queue
			.Jobs.Where(job => job.State is JobState.Waiting or JobState.Queued or JobState.Running)
			.Select(job => job.Request)
			.OfType<RunFeed>()
			.Select(feed => feed.Id)
			.ToHashSet();

	internal static IReadOnlyList<UpNextRow> OnASchedule(IReadOnlyList<Feed> feeds, DateTimeOffset from, TimeZoneInfo zone)
	{
		return
		[
			.. feeds
				.Where(feed => feed.Enabled)
				.Select(feed => feed.Schedule.NextRun(from, zone).Map(next => (feed, next)))
				.WhereSome()
				.OrderBy(entry => entry.next)
				.Select(entry => new UpNextRow(
					entry.feed.Name,
					ScheduleText.Describe(entry.feed.Schedule),
					ScheduleText.Away(entry.next, from),
					UpNextKind.Scheduled,
					EditHref(entry.feed.Id)
				)
				{
					Brands = [entry.feed.Provider.AsMediaProvider()],
				}),
		];
	}

	private static UpNextRow Row(
		PendingJob job,
		IReadOnlyDictionary<FeedId, string> feedNames,
		IReadOnlyDictionary<FeedId, MediaProvider> feedBrands,
		IReadOnlyList<MediaProvider> sinkProviders
	)
	{
		var name = Name(job.Request, feedNames);
		var href = Href(job.Request, feedNames);
		var brands = Brands(job.Request, feedBrands, sinkProviders);
		var deleted = Deleted(job.Request, feedNames);
		if (job.State is JobState.Running)
		{
			return new UpNextRow(name, "running now", "running", UpNextKind.Running, href, deleted) { Job = Some.Of(job.Id), Brands = brands };
		}
		if (job.State is JobState.Waiting)
		{
			return new UpNextRow(name, Waiting(job.Request), string.Empty, UpNextKind.Waiting, href, deleted)
			{
				RunsAt = Some.Of(job.RunAfter),
				Job = Some.Of(job.Id),
				Brands = brands,
			};
		}
		return new UpNextRow(name, "queued", string.Empty, UpNextKind.Queued, href, deleted) { Job = Some.Of(job.Id), Brands = brands };
	}

	private static IReadOnlyList<MediaProvider> Brands(
		IJobRequest request,
		IReadOnlyDictionary<FeedId, MediaProvider> feedBrands,
		IReadOnlyList<MediaProvider> sinkProviders
	)
	{
		return request switch
		{
			RunFeed feed => feedBrands.GetValueOrNone(feed.Id).Match(provider => (IReadOnlyList<MediaProvider>)[provider], () => []),
			SyncWantlist => sinkProviders,
			_ => [],
		};
	}

	private static string Waiting(IJobRequest request) => request is SyncWantlist ? "waiting for grace period" : "waiting";

	private static bool Deleted(IJobRequest request, IReadOnlyDictionary<FeedId, string> feedNames) => request is RunFeed feed && !feedNames.ContainsKey(feed.Id);

	private static string? Href(IJobRequest request, IReadOnlyDictionary<FeedId, string> feedNames) =>
		request is RunFeed feed && feedNames.ContainsKey(feed.Id) ? EditHref(feed.Id) : null;

	private static string EditHref(FeedId feedId) => $"/feeds/edit/{UrlGuid.Encode(feedId.Value)}";

	private static string Name(IJobRequest request, IReadOnlyDictionary<FeedId, string> feedNames) =>
		request switch
		{
			RunFeed feed => FeedLabel.Of(feed.Id, feedNames),
			SyncWantlist => "Sync Wantlist",
			_ => request.Kind.GetDescription(),
		};
}
