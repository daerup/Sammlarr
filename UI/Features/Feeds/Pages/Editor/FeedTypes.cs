using BlazorBlueprint.Primitives.Filtering;
using Domain.Feed;
using Domain.Feed.Run;
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

public enum PreviewBucket
{
	Included,
	Bypassed,
	Filtered,
	Skipped,
	Unresolved,
}

public static class PreviewResultFilter
{
	public static PreviewBucket Bucket(QueryVerdict<MediaLookup> verdict) =>
		verdict.Outcome switch
		{
			QueryOutcome.Culled => PreviewBucket.Filtered,
			QueryOutcome.Truncated => PreviewBucket.Skipped,
			QueryOutcome.Qualified => verdict.Bypassed ? PreviewBucket.Bypassed : PreviewBucket.Included,
		};

	public static bool IsVisible(PreviewBucket bucket, IReadOnlyCollection<PreviewBucket> active) => active.Count == 0 || active.Contains(bucket);
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
