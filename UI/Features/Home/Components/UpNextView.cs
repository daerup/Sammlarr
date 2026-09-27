using Contracts.Jobs;
using Domain.Feed;
using Domain.Jobs;
using UI.Features.Feeds.Components;
using UrlGuid = global::Shared.UrlGuid;

namespace UI.Features.Home.Components;

public enum UpNextKind
{
	Running,
	Queued,
	Scheduled,
}

public sealed record UpNextRow(string Name, string Detail, string When, UpNextKind Kind, string? Href);

internal static class UpNextView
{
	internal static IReadOnlyList<UpNextRow> InTheQueue(QueueSnapshot queue, IReadOnlyDictionary<FeedId, string> feedNames, string providerName) =>
		[.. queue.Jobs.Where(job => job.State is JobState.Queued or JobState.Running).Select((job, index) => Row(job, index, feedNames, providerName))];

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
					EditHref(entry.feed.Id)
				)),
		];

	private static UpNextRow Row(PendingJob job, int index, IReadOnlyDictionary<FeedId, string> feedNames, string providerName) =>
		job.State is JobState.Running
			? new UpNextRow(Name(job.Request, feedNames, providerName), "running now", "running", UpNextKind.Running, Href(job.Request, feedNames))
			: new UpNextRow(Name(job.Request, feedNames, providerName), "queued", $"{Ordinal(index + 1)} in line", UpNextKind.Queued, Href(job.Request, feedNames));

	private static string? Href(IJobRequest request, IReadOnlyDictionary<FeedId, string> feedNames) =>
		request is RunFeed feed && feedNames.ContainsKey(feed.Id) ? EditHref(feed.Id) : null;

	private static string EditHref(FeedId feedId) => $"/feeds/edit/{UrlGuid.Encode(feedId.Value)}";

	private static string Name(IJobRequest request, IReadOnlyDictionary<FeedId, string> feedNames, string providerName) =>
		request switch
		{
			RunFeed feed => FeedLabel.Of(feed.Id, feedNames),
			SyncWatchlist => $"Sync to {providerName}",
			_ => request.Kind.GetDescription(),
		};

	internal static string Ordinal(int position) =>
		position switch
		{
			1 => "1st",
			2 => "2nd",
			3 => "3rd",
			_ when position % 10 is 1 && position % 100 is not 11 => $"{position}st",
			_ when position % 10 is 2 && position % 100 is not 12 => $"{position}nd",
			_ when position % 10 is 3 && position % 100 is not 13 => $"{position}rd",
			_ => $"{position}th",
		};
}
