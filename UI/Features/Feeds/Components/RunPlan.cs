using Domain.Feed;
using Domain.Feed.Run;

namespace UI.Features.Feeds.Components;

public enum RunGroup
{
	WorthRunning,
	SoonAnyway,
	ManualOnly,
}

public enum RunFlag
{
	Disabled,
	Failed,
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

	internal static IReadOnlyList<RunCandidate> Candidates(IReadOnlyList<FeedOverview> feeds, DateTimeOffset from, TimeZoneInfo zone) =>
		[
			.. feeds
				.Select(overview => Candidate(overview, from, zone))
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
			RunFlag.Failed => "Last run failed",
			RunFlag.Inconclusive => "Last run was inconclusive",
			RunFlag.NotDue => "Not due for a while",
		};

	private static RunCandidate Candidate(FeedOverview overview, DateTimeOffset from, TimeZoneInfo zone)
	{
		var feed = overview.Feed;
		var next = feed.Schedule is Scheduled scheduled ? scheduled.NextRun(from, zone) : null;
		var stalled = overview.LastRun is Some<FeedRun>({ Conclusiveness: not Conclusiveness.Conclusive });

		var group =
			feed.Schedule is Manual ? RunGroup.ManualOnly
			: stalled || next is null || next - from > Soon ? RunGroup.WorthRunning
			: RunGroup.SoonAnyway;

		return new RunCandidate(
			feed.Id,
			feed.Name,
			ScheduleText.Describe(feed.Schedule),
			Outcome(overview.LastRun),
			!feed.Enabled ? RunFlag.Disabled
				: overview.LastRun is Some<FeedRun>({ Failed: true }) ? RunFlag.Failed
				: stalled ? RunFlag.Inconclusive
				: group is RunGroup.WorthRunning && next is not null ? RunFlag.NotDue
				: null,
			feed.Enabled ? next : null,
			group,
			feed.Enabled
		);
	}

	private static LastRun Outcome(Optional<FeedRun> last) =>
		last is Some<FeedRun>(var run) ? new LastRun(true, run.Counts.Added, run.Counts.Removed) : new LastRun(false, 0, 0);
}
