using Domain.Feed;
using Domain.Feed.Run;
using Domain.Jobs;
using Domain.Providers;

namespace UI.Features.Feeds.Pages;

public enum FeedStatus
{
	Enabled,
	Paused,
	Busy,
}

public enum FeedTriggerKind
{
	Scheduled,
	Manual,
}

public enum FeedStrategyKind
{
	Append,
	Mirror,
}

public enum FeedSort
{
	Name,
	RecentlyRun,
	NextRun,
	MostOnWantlist,
	RecentlyAdded,
}

public sealed record ProviderCount(FeedProvider Provider, int Count);

public sealed record FeedRow(FeedOverview Overview, JobState? Job, Optional<DateTimeOffset> NextRun)
{
	public Feed Feed => Overview.Feed;

	public int OnWantlist => Overview.OnWantlist;

	public bool IsBusy => Job is JobState.Waiting or JobState.Queued or JobState.Running;

	public bool Is(FeedStatus status) =>
		status switch
		{
			FeedStatus.Enabled => Feed.Enabled,
			FeedStatus.Paused => !Feed.Enabled,
			FeedStatus.Busy => IsBusy,
		};

	public bool Is(FeedTriggerKind trigger) =>
		trigger switch
		{
			FeedTriggerKind.Scheduled => Feed.Schedule is Scheduled,
			FeedTriggerKind.Manual => Feed.Schedule is Manual,
		};

	public bool Is(FeedStrategyKind strategy) =>
		strategy switch
		{
			FeedStrategyKind.Append => Feed.Strategy is Append,
			FeedStrategyKind.Mirror => Feed.Strategy is Mirror,
		};

	public static FeedRow Of(FeedOverview overview, IReadOnlyDictionary<FeedId, JobState> jobs, DateTimeOffset now, TimeZoneInfo zone)
	{
		return new(
			overview,
			jobs.TryGetValue(overview.Feed.Id, out var job) ? job : null,
			overview.Feed.Enabled && overview.Feed.Schedule is Scheduled scheduled ? scheduled.NextRun(now, zone) : new None()
		);
	}
}

public sealed record FeedFilter
{
	public static readonly FeedFilter None = new();

	public IReadOnlyList<FeedProvider> Providers { get; init; } = [];
	public IReadOnlyList<FeedStatus> Statuses { get; init; } = [];
	public IReadOnlyList<FeedTriggerKind> Triggers { get; init; } = [];
	public IReadOnlyList<FeedStrategyKind> Strategies { get; init; } = [];
	public string? Search { get; init; }
	public FeedSort Sort { get; init; } = FeedSort.Name;

	public int ActiveCount =>
		(Providers.Count > 0 ? 1 : 0)
		+ (Statuses.Count > 0 ? 1 : 0)
		+ (Triggers.Count > 0 ? 1 : 0)
		+ (Strategies.Count > 0 ? 1 : 0)
		+ (string.IsNullOrWhiteSpace(Search) ? 0 : 1);

	public bool HasFilters => ActiveCount > 0;

	public FeedFilter ClearFilters() => new() { Sort = Sort };

	public IReadOnlyList<FeedRow> Apply(IEnumerable<FeedRow> rows) => [.. SortBy(rows.Where(Matches))];

	private bool Matches(FeedRow row) =>
		(Providers.Count == 0 || Providers.Contains(row.Feed.Provider))
		&& (Statuses.Count == 0 || Statuses.Any(row.Is))
		&& (Triggers.Count == 0 || Triggers.Any(row.Is))
		&& (Strategies.Count == 0 || Strategies.Any(row.Is))
		&& MatchesSearch(row.Feed);

	private bool MatchesSearch(Feed feed) =>
		string.IsNullOrWhiteSpace(Search)
		|| feed.Name.Contains(Search.Trim(), StringComparison.CurrentCultureIgnoreCase)
		|| feed.Identifier.Contains(Search.Trim(), StringComparison.CurrentCultureIgnoreCase);

	private IEnumerable<FeedRow> SortBy(IEnumerable<FeedRow> rows)
	{
		return Sort switch
		{
			FeedSort.Name => rows.OrderBy(row => row.Feed.Name, StringComparer.CurrentCultureIgnoreCase),
			FeedSort.RecentlyRun => rows.OrderBy(row => row.Overview.LastRun.IsNone())
				.ThenByDescending(row => row.Overview.LastRun.Map(run => run.StartedAt).ToNullable()),
			FeedSort.NextRun => rows.OrderBy(row => row.NextRun.IsNone()).ThenBy(row => row.NextRun.ToNullable()),
			FeedSort.MostOnWantlist => rows.OrderByDescending(row => row.OnWantlist).ThenBy(row => row.Feed.Name, StringComparer.CurrentCultureIgnoreCase),
			FeedSort.RecentlyAdded => rows.OrderByDescending(row => row.Feed.CreatedAt),
		};
	}
}

internal static class FeedFilterText
{
	internal static string Label(FeedStatus status) =>
		status switch
		{
			FeedStatus.Enabled => "Enabled",
			FeedStatus.Paused => "Paused",
			FeedStatus.Busy => "Running or queued",
		};

	internal static string Label(FeedTriggerKind trigger) =>
		trigger switch
		{
			FeedTriggerKind.Scheduled => "On a schedule",
			FeedTriggerKind.Manual => "Manual",
		};

	internal static string Label(FeedStrategyKind strategy) =>
		strategy switch
		{
			FeedStrategyKind.Append => "Append",
			FeedStrategyKind.Mirror => "Mirror",
		};

	internal static string Label(FeedSort sort) =>
		sort switch
		{
			FeedSort.Name => "Name A to Z",
			FeedSort.RecentlyRun => "Recently run",
			FeedSort.NextRun => "Next run",
			FeedSort.MostOnWantlist => "Most on wantlist",
			FeedSort.RecentlyAdded => "Recently added",
		};
}

internal static class FeedPulse
{
	internal static int Running(IEnumerable<FeedRow> rows) => rows.Count(row => row.Job is JobState.Running);

	internal static int Queued(IEnumerable<FeedRow> rows) => rows.Count(row => row.Job is JobState.Waiting or JobState.Queued);

	internal static FeedRow? Next(IEnumerable<FeedRow> rows) => rows.Where(row => !row.IsBusy && row.NextRun.IsSome()).MinBy(row => row.NextRun.ToNullable());

	internal static (int Added, int Removed) Moved(IEnumerable<FeedRun> runs) => (runs.Sum(run => run.Counts.Added), runs.Sum(run => run.Counts.Removed));

	internal static IReadOnlyList<ProviderCount> Providers(IEnumerable<FeedRow> rows) =>
		[.. rows.GroupBy(row => row.Feed.Provider).OrderBy(group => group.Key).Select(group => new ProviderCount(group.Key, group.Count()))];
}
