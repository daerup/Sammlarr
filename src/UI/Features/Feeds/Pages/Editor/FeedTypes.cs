using BlazorBlueprint.Primitives.Filtering;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Processing;
using Domain.Providers;
using UI.Features.Feeds.Pages.Editor.Filters;

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
			Culled => PreviewBucket.Filtered,
			Truncated => PreviewBucket.Skipped,
			Qualified => OfQualified(verdict),
		};
	}

	public static bool IsVisible(PreviewBucket bucket, IReadOnlyCollection<PreviewBucket> active) => active.Count == 0 || active.Contains(bucket);

	private static PreviewBucket OfQualified(QueryVerdict<MatchedMedia> verdict)
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

	internal const string PastTheLimitReason = "Your import limit no longer reaches it.";

	internal static AbsencePhrase Absence => new("missed", "of", "run");

	internal static string AtRiskReason(QueryVerdict<MatchedMedia> verdict)
	{
		return verdict.Outcome switch
		{
			Culled => "It no longer matches your filter rules.",
			Truncated => PastTheLimitReason,
			Qualified => string.Empty,
		};
	}

	internal static string Fields(MediaFields fields)
	{
		string[] details = [.. Enum.GetValues<MediaField>().Where(field => field is not MediaField.None && fields.Details.HasFlag(field)).Select(Label).Distinct()];
		return string.Join(", ", fields.Ratings.Count > 0 ? [.. details, "Ratings"] : details);
	}

	private static string Label(MediaField field)
	{
		return field switch
		{
			MediaField.None => string.Empty,
			MediaField.Title => "Title",
			MediaField.Tagline => "Tagline",
			MediaField.Summary => "Summary",
			MediaField.Poster => "Poster",
			MediaField.Backdrop => "Backdrop",
			MediaField.ReleaseDate => "Release Date",
			MediaField.Genres => "Genres",
			MediaField.Runtime or MediaField.RuntimePerEpisode => "Runtime",
			MediaField.Seasons => "Seasons",
			MediaField.Episodes => "Episodes",
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
	public bool IsListMissing { get; init; }

	public Optional<int> MirrorExposure { get; init; } = new None();
}

internal static class MirrorSwitch
{
	internal const string Heading = "Mirror removes titles this feed added";

	internal const string Confirm = "Switch to Mirror";

	public static bool NeedsConfirmation(FeedStrategy stored, FeedStrategy pending, Optional<int> exposure)
	{
		return stored is Append && pending is Mirror && exposure.Map(count => count > 0).GetValueOr(true);
	}

	public static string Warning(Optional<int> exposure, int threshold)
	{
		return exposure.Match(
			count => Known(count, threshold),
			() =>
				$"The preview hasn't finished, so it can't tell yet how many titles have already left the list. Those are deleted {Timing(threshold, single: false)}."
		);
	}

	private static string Known(int exposure, int threshold)
	{
		var single = exposure == 1;
		var left = single ? "1 has already left the list" : $"{exposure} have already left the list";
		return $"{left}, and {(single ? "is" : "are")} deleted {Timing(threshold, single)}.";
	}

	private static string Timing(int threshold, bool single) => threshold == 1 ? "on the next run" : $"after {threshold} runs without {(single ? "it" : "them")}";
}

public sealed record IdentifierCheck(string Typed, Result<string> Answer)
{
	public bool Refused => Answer.Match(_ => false, error => !Unreachable(error));

	public static bool Unreachable(Error error) => error.CausedBy<RemoteUnavailableException>().IsSome();
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

	public Optional<string> Problem =>
		this switch
		{
			{ Name: var name } when string.IsNullOrWhiteSpace(name) => Some.Of("Name is required"),
			{ Provider: null } => Some.Of("Pick a provider"),
			{ IsProviderAvailable: false } => Some.Of("The provider's API key is missing"),
			{ Identifier: var identifier } when string.IsNullOrWhiteSpace(identifier) => Some.Of("List identifier is required"),
			{ Limit.Count: <= 0 } => Some.Of("Limit must be a positive whole number"),
			{ IsScheduleValid: false } => Some.Of("The schedule is incomplete"),
			_ => FilterDefinitionConverter.Incomplete(Filter).Map(condition => $"{FeedFields.LabelOf(condition.Field)} condition unfinished"),
		};

	public bool SameSource(FeedConfig other) => Provider == other.Provider && Identifier.Trim() == other.Identifier.Trim();

	public int GetPreviewHash() =>
		HashCode.Combine(HashCode.Combine(Provider, Identifier, Filter is not null, Filter?.Version), HashCode.Combine(Sort, Limit, Strategy, RatingStrategy));

	public int GetSaveHash() =>
		HashCode.Combine(
			HashCode.Combine(IsEnabled, Name, Provider, Strategy, Identifier),
			HashCode.Combine(Filter is not null, Filter?.Version, Sort, Limit),
			HashCode.Combine(Schedule, RatingStrategy)
		);
}
