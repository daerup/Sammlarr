using Contracts.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Providers;
using UI.Features.Feeds.Components;
using UI.Shared.Common;
using UI.Shared.Services.Branding;

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
	public IReadOnlyList<BrandingInfo> Brands { get; init; } = [];

	public Optional<DateTimeOffset> RunsAt { get; init; } = new None();

	public Optional<JobId> Job { get; init; } = new None();

	public string CancelNote { get; init; } = string.Empty;

	public bool CanCancel => Job.IsSome() && Kind is UpNextKind.Queued or UpNextKind.Waiting;
}

internal static class UpNextView
{
	private const string StartsAgainWhenAsked = "It leaves the queue without running. You can start it again at any time, and schedules keep running as usual.";

	private const string StartsAgainByItself =
		"It leaves the queue without running. It starts again by itself after the next feed run, a new key or a Plex sign-in.";

	internal static IReadOnlyList<UpNextRow> InTheQueue(
		QueueSnapshot queue,
		IReadOnlyDictionary<FeedId, string> feedNames,
		IReadOnlyDictionary<FeedId, IReadOnlyList<BrandingInfo>> feedBrands,
		IReadOnlyList<BrandingInfo> sinkBrands
	)
	{
		return
		[
			.. queue
				.Jobs.Where(job => job.State is JobState.Waiting or JobState.Queued or JobState.Running)
				.Select(job => Row(job, feedNames, feedBrands, sinkBrands)),
		];
	}

	internal static IReadOnlySet<FeedId> FeedsAlreadyPending(QueueSnapshot queue) =>
		queue
			.Jobs.Where(job => job.State is JobState.Waiting or JobState.Queued or JobState.Running)
			.Select(job => job.Request)
			.OfType<RunFeed>()
			.Select(feed => feed.Id)
			.ToHashSet();

	internal static IReadOnlyList<UpNextRow> OnASchedule(IReadOnlyList<Feed> feeds, DateTimeOffset from, TimeZoneInfo zone, MediaProviderBrandingService branding)
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
					Brands = branding.Brands(entry.feed.Provider.Service),
				}),
		];
	}

	private static UpNextRow Row(
		PendingJob job,
		IReadOnlyDictionary<FeedId, string> feedNames,
		IReadOnlyDictionary<FeedId, IReadOnlyList<BrandingInfo>> feedBrands,
		IReadOnlyList<BrandingInfo> sinkBrands
	)
	{
		var name = Name(job.Request, feedNames);
		var href = Href(job.Request, feedNames);
		var brands = Brands(job.Request, feedBrands, sinkBrands);
		var deleted = Deleted(job.Request, feedNames);
		var note = CancelNote(job.Request);
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
				CancelNote = note,
			};
		}
		return new UpNextRow(name, "queued", string.Empty, UpNextKind.Queued, href, deleted)
		{
			Job = Some.Of(job.Id),
			Brands = brands,
			CancelNote = note,
		};
	}

	private static IReadOnlyList<BrandingInfo> Brands(
		IJobRequest request,
		IReadOnlyDictionary<FeedId, IReadOnlyList<BrandingInfo>> feedBrands,
		IReadOnlyList<BrandingInfo> sinkBrands
	)
	{
		return request switch
		{
			RunFeed feed => feedBrands.GetValueOrNone(feed.Id).GetValueOr([]),
			SyncWantlist => sinkBrands,
			_ => [],
		};
	}

	private static string CancelNote(IJobRequest request) => request is FillDetails ? StartsAgainByItself : StartsAgainWhenAsked;

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
