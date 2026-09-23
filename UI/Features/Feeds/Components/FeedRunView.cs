using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;
using UI.Shared.Changes;

namespace UI.Features.Feeds.Components;

internal static class FeedRunView
{
	internal static IReadOnlyList<ChangeEntry> Entries(FeedRun run, IReadOnlyDictionary<MediaId, MediaLookup> media) =>
		[
			.. run
				.Items.Where(item => item.State is not FeedRunItemState.Kept)
				.Where(item => media.ContainsKey(item.MediaId))
				.Select(item => new ChangeEntry
				{
					Title = media[item.MediaId] is IdentifiedMedia named ? named.Title : string.Empty,
					Id = item.MediaId,
					Media = media[item.MediaId],
					Kind = item.State is FeedRunItemState.Removed ? ChangeKind.Removed : ChangeKind.Added,
					Subtitle = Subtitle(media[item.MediaId]),
					ContextIcon = item.State is FeedRunItemState.Removed ? "log-out" : null,
					ContextTooltip = item.State is FeedRunItemState.Removed ? "Removed once it had been absent from enough runs" : null,
				}),
		];

	internal static IReadOnlyList<ChangeEntry> Entries(IFeedPlan summary, IReadOnlyDictionary<MediaId, MediaLookup> media) =>
		[
			.. summary.Delta.Drops.Where(media.ContainsKey).Select(id => Pending(id, media[id], ChangeKind.Removed)),
			.. summary.Delta.Adds.Where(media.ContainsKey).Select(id => Pending(id, media[id], ChangeKind.Added)),
		];

	private static ChangeEntry Pending(MediaId id, MediaLookup item, ChangeKind kind) =>
		new()
		{
			Title = item is IdentifiedMedia named ? named.Title : string.Empty,
			Id = id,
			Media = item,
			Kind = kind,
			Subtitle = Subtitle(item),
			ContextIcon = kind is ChangeKind.Removed ? "log-out" : null,
			ContextTooltip = kind is ChangeKind.Removed ? "Absent long enough that this run will remove it" : null,
		};

	internal static string Pending(IFeedPlan summary)
	{
		var parts = new List<string> { $"{summary.Verdicts.Count + summary.Unresolved.Count} fetched" };

		if (summary.Conclusiveness is not Conclusiveness.Conclusive)
		{
			parts.Add("removals held back");
		}

		return string.Join(" · ", parts);
	}

	internal static IReadOnlyList<ChangeChip> Chips(FeedRunCounts counts) =>
		[
			new ChangeChip
			{
				Icon = "list",
				Count = counts.Total,
				Tooltip = "Everything the source returned",
			},
			new ChangeChip
			{
				Glyph = "+",
				Count = counts.Added,
				Tooltip = "Added to your collection",
				Tone = ChipTone.Added,
				Filters = ChangeKind.Added,
			},
			new ChangeChip
			{
				Glyph = "−",
				Count = counts.Removed,
				Tooltip = "Removed from your collection",
				Tone = ChipTone.Removed,
				Filters = ChangeKind.Removed,
			},
			new ChangeChip
			{
				Icon = "bookmark-check",
				Count = counts.Kept,
				Tooltip = "Already yours, nothing to do",
				StartsCountsOnly = true,
			},
			new ChangeChip
			{
				Icon = "redo-dot",
				Count = counts.Bypassed,
				Tooltip = "Bypassed import",
			},
			new ChangeChip
			{
				Icon = "filter-x",
				Count = counts.Filtered,
				Tooltip = "Excluded by your filter rules",
			},
			new ChangeChip
			{
				Icon = "scissors",
				Count = counts.Skipped,
				Tooltip = "Excluded by your import limit",
			},
			new ChangeChip
			{
				Icon = "circle-alert",
				Count = counts.Unresolved,
				Tooltip = "Could not be matched in your media provider",
			},
		];

	internal static string Summary(FeedRun run)
	{
		var parts = new List<string> { run.Trigger is Domain.Feed.Scheduled ? "scheduled" : "manual" };

		if (run.Counts.Total > 0)
		{
			parts.Add($"{run.Counts.Total} fetched");
		}

		parts.Add($"took {run.Duration.TotalSeconds:0.0} s");

		return string.Join(" · ", parts);
	}

	private static string Subtitle(MediaLookup media) =>
		media is IdentifiedMedia identified && identified.ReleaseDate is { } released ? released.Year.ToString() : string.Empty;
}
