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
	Added,
	Kept,
	Bypassed,
	Filtered,
	Skipped,
	Unmatched,
}

public static class PreviewResultFilter
{
	public static PreviewBucket Bucket(QueryVerdict<MatchedMedia> verdict)
	{
		return verdict.Outcome switch
		{
			QueryOutcome.Culled => PreviewBucket.Filtered,
			QueryOutcome.Truncated => PreviewBucket.Skipped,
			QueryOutcome.Qualified => Qualified(verdict),
		};
	}

	public static bool IsVisible(PreviewBucket bucket, IReadOnlyCollection<PreviewBucket> active) => active.Count == 0 || active.Contains(bucket);

	private static PreviewBucket Qualified(QueryVerdict<MatchedMedia> verdict)
	{
		return verdict.Kept ? PreviewBucket.Kept
			: verdict.WillImport ? PreviewBucket.Added
			: PreviewBucket.Bypassed;
	}
}

internal static class PreviewCopy
{
	internal const string GoneHeading = "No longer on the list";

	internal const string LeavingHeading = "On its way out";

	internal const string GoneReason = "The source list no longer contains it.";

	internal static AbsencePhrase Absence => new("Absent from", "of", "runs");

	internal static string OutcomeIcon(QueryVerdict<MatchedMedia> verdict)
	{
		return verdict.Outcome switch
		{
			QueryOutcome.Culled => "filter-x",
			QueryOutcome.Truncated => "scissors",
			QueryOutcome.Qualified => "circle-help",
		};
	}

	internal static string AtRiskReason(QueryVerdict<MatchedMedia> verdict)
	{
		return verdict.Outcome switch
		{
			QueryOutcome.Culled => "It no longer matches your filter rules.",
			QueryOutcome.Truncated => "Your import limit no longer reaches it.",
			QueryOutcome.Qualified => string.Empty,
		};
	}
}

internal sealed record AbsencePhrase(string Lead, string Join, string Tail);

internal sealed record GoneItem(MatchedMedia Media);

public record PreviewStatus
{
	public bool IsLoading { get; init; }
	public bool HasResults { get; init; }
	public bool HasFailed { get; init; }
	public bool EarlySaveAllowed { get; init; }
	public int MirrorExposure { get; init; }
}

internal static class MirrorSwitch
{
	internal const string Heading = "Mirror removes titles this feed added";

	internal const string Confirm = "Switch to Mirror";

	public static bool NeedsConfirmation(FeedStrategy stored, FeedStrategy pending, int exposure) => stored is Append && pending is Mirror && exposure > 0;

	public static string Warning(int exposure, int threshold)
	{
		var single = exposure == 1;
		var left = single ? "1 has already left the list" : $"{exposure} have already left the list";
		var timing = threshold == 1 ? "on the next run" : $"after {threshold} runs without {(single ? "it" : "them")}";
		return $"{left}, and {(single ? "is" : "are")} deleted {timing}.";
	}
}

public record FeedConfig
{
	public bool IsEnabled { get; init; } = true;
	public string Name { get; init; } = string.Empty;
	public FeedProvider? Provider { get; init; }
	public FeedStrategy Strategy { get; init; } = new(new Append());
	public string Identifier { get; init; } = string.Empty;
	public FilterDefinition? Filter { get; init; }
	public QuerySort? Sort { get; init; }
	public QueryLimit? Limit { get; init; }
	public FeedRunTrigger Schedule { get; init; } = new FeedRunTrigger(new Manual());
	public Optional<RatingStrategy> RatingStrategy { get; init; } = new None();

	public bool IsScheduleValid { get; init; } = true;

	public bool IsProviderAvailable { get; init; } = true;

	public bool IsTestable => Provider is not null && IsProviderAvailable && !string.IsNullOrWhiteSpace(Identifier) && (Limit is null || Limit.Count > 0);

	public bool IsValid => IsTestable && !string.IsNullOrWhiteSpace(Name) && IsScheduleValid;

	public int GetPreviewHash() =>
		HashCode.Combine(HashCode.Combine(Provider, Identifier, Filter is not null, Filter?.Version), HashCode.Combine(Sort, Limit, Strategy, RatingStrategy));

	public int GetSaveHash() =>
		HashCode.Combine(
			HashCode.Combine(IsEnabled, Name, Provider, Strategy, Identifier),
			HashCode.Combine(Filter is not null, Filter?.Version, Sort, Limit),
			HashCode.Combine(Schedule, RatingStrategy)
		);
}
