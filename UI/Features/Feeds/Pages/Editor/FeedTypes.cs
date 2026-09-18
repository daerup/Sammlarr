using BlazorBlueprint.Primitives.Filtering;
using Contracts.Processing;
using Domain.Feed;
using Domain.Media;
using Domain.Processing;
using Domain.Providers;

namespace UI.Features.Feeds.Pages.Editor;

public enum ScheduleMode
{
	Manual,
	Scheduled,
}

public enum StrategyMode
{
	Append,
	Mirror,
}

public static class PreviewResultFilter
{
	public static readonly Func<QueryResult<MediaQueryRow>, bool> Included = result => result is MediaQueryRow row && row.Media is not UnmatchedMedia;
	public static readonly Func<QueryResult<MediaQueryRow>, bool> Bypassed = result => result is Bypassed<MediaQueryRow>;
	public static readonly Func<QueryResult<MediaQueryRow>, bool> Filtered = result => result is Culled<MediaQueryRow>;
	public static readonly Func<QueryResult<MediaQueryRow>, bool> Skipped = result => result is Truncated<MediaQueryRow>;
	public static readonly Func<QueryResult<MediaQueryRow>, bool> Unresolved = result => result is MediaQueryRow row && row.Media is UnmatchedMedia;

	public static bool IsVisible(QueryResult<MediaQueryRow> result, IReadOnlyCollection<Func<QueryResult<MediaQueryRow>, bool>> active) =>
		active.Count == 0 || active.Any(matches => matches(result));
}

public record PreviewStatus
{
	public bool IsLoading { get; init; }
	public bool HasResults { get; init; }
	public bool HasFailed { get; init; }
	public bool HasEnrollmentChanges { get; init; }
	public bool EarlySaveAllowed { get; init; }
}

public record FeedConfig
{
	public bool IsEnabled { get; init; } = true;
	public string Name { get; init; } = string.Empty;
	public FeedProvider? Provider { get; init; } = FeedProvider.Mdblist;
	public FeedStrategy Strategy { get; init; } = new(new Append());
	public string Identifier { get; init; } = string.Empty;
	public FilterDefinition? Filter { get; init; }
	public QuerySort? Sort { get; init; }
	public QueryLimit? Limit { get; init; }
	public SyncMode Schedule { get; init; } = new SyncMode(new Manual());

	public bool IsScheduleValid { get; init; } = true;

	public bool IsStrategyValid { get; init; } = true;

	public bool IsTestable => Provider is not null && !string.IsNullOrWhiteSpace(Identifier) && (Limit is null || Limit.Count > 0);

	public bool IsValid => IsTestable && !string.IsNullOrWhiteSpace(Name) && IsScheduleValid && IsStrategyValid;

	public int GetPreviewHash() => HashCode.Combine(HashCode.Combine(Provider, Identifier, Filter is not null, Filter?.Version), HashCode.Combine(Sort, Limit));

	public int GetSaveHash() =>
		HashCode.Combine(
			HashCode.Combine(IsEnabled, Name, Provider, Strategy, Identifier),
			HashCode.Combine(Filter is not null, Filter?.Version, Sort, Limit),
			Schedule
		);
}
