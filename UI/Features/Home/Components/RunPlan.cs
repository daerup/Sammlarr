using Domain.Feed;
using Domain.Feed.Run;
using UI.Features.Feeds.Components;

namespace UI.Features.Home.Components;

public enum RunGroup
{
	WorthRunning,
	SoonAnyway,
	ManualOnly,
}

public enum RunFlag
{
	Disabled,
	Inconclusive,
	NotDue,
}

public sealed record LastRun(bool EverRan, int Added, int Removed)
{
	internal bool Changed => Added > 0 || Removed > 0;
}

public sealed record RunCandidate(
	FeedId Id,
	string Name,
	string Schedule,
	LastRun LastRun,
	RunFlag? Flag,
	DateTimeOffset? NextRun,
	RunGroup Group,
	bool Enabled
);

internal static class RunPlan
{
	private static readonly TimeSpan Soon = TimeSpan.FromHours(24);

	internal static IReadOnlyList<RunCandidate> Candidates(
		IReadOnlyList<Feed> feeds,
		IReadOnlyDictionary<FeedId, FeedOverview> overviews,
		DateTimeOffset from,
		TimeZoneInfo zone
	) =>
		[
			.. feeds
				.Select(feed => Candidate(feed, overviews.GetValueOrDefault(feed.Id), from, zone))
				.OrderBy(candidate => candidate.Enabled ? 0 : 1)
				.ThenBy(candidate => candidate.Group)
				.ThenBy(candidate => candidate.NextRun ?? DateTimeOffset.MaxValue)
				.ThenBy(candidate => candidate.Name, StringComparer.CurrentCultureIgnoreCase),
		];

	internal static IReadOnlySet<FeedId> DefaultSelection(IEnumerable<RunCandidate> candidates) =>
		candidates.Where(candidate => candidate.Enabled && candidate.Group is RunGroup.WorthRunning).Select(candidate => candidate.Id).ToHashSet();

	internal static string Describe(RunGroup group) =>
		group switch
		{
			RunGroup.WorthRunning => "Failed last time, or not due for days",
			RunGroup.SoonAnyway => "Scheduled within the day",
			RunGroup.ManualOnly => "Never runs on its own",
		};

	internal static string Label(RunGroup group) =>
		group switch
		{
			RunGroup.WorthRunning => "Worth running now",
			RunGroup.SoonAnyway => "Running again soon anyway",
			RunGroup.ManualOnly => "Manual only",
		};

	internal static string Label(RunFlag flag) =>
		flag switch
		{
			RunFlag.Disabled => "Disabled",
			RunFlag.Inconclusive => "Last run was inconclusive",
			RunFlag.NotDue => "Not due for a while",
		};

	private static RunCandidate Candidate(Feed feed, FeedOverview? overview, DateTimeOffset from, TimeZoneInfo zone)
	{
		var next = feed.Schedule is Scheduled scheduled ? scheduled.NextRun(from, zone) : null;
		var stalled = overview is not null && overview.LastRun.Conclusiveness is not Conclusiveness.Conclusive;

		var group =
			feed.Schedule is Manual ? RunGroup.ManualOnly
			: stalled || next is null || next - from > Soon ? RunGroup.WorthRunning
			: RunGroup.SoonAnyway;

		return new RunCandidate(
			feed.Id,
			feed.Name,
			ScheduleText.Describe(feed.Schedule),
			Outcome(overview),
			!feed.Enabled ? RunFlag.Disabled
				: stalled ? RunFlag.Inconclusive
				: group is RunGroup.WorthRunning && next is not null ? RunFlag.NotDue
				: null,
			next,
			group,
			feed.Enabled
		);
	}

	private static LastRun Outcome(FeedOverview? overview) =>
		overview is null ? new LastRun(false, 0, 0) : new LastRun(true, overview.LastRun.Counts.Added, overview.LastRun.Counts.Removed);
}
