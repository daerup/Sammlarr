using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;
using UI.Shared.Changes;
using UI.Shared.Formatting;
using UI.Shared.Media;

namespace UI.Features.Feeds.Components.Runs;

internal static class FeedRunView
{
	internal const string TitlesGone = "Older runs keep their counts; the titles they added and removed are no longer listed.";

	internal static bool LostItsTitles(FeedRun run) => run.Items.Count == 0 && run.Counts.Added + run.Counts.Removed > 0;

	internal static IReadOnlyList<ChangeItem> Items(FeedRun run, IReadOnlyDictionary<MediaId, MatchedMedia> media)
	{
		return
		[
			.. run
				.Items.Where(item => media.ContainsKey(item.MediaId))
				.Select(item => new ChangeItem
				{
					Title = media[item.MediaId].Details.Title ?? string.Empty,
					Id = item.MediaId,
					Media = media[item.MediaId],
					Kind = Kind(item.State),
					Subtitle = Subtitle(media[item.MediaId]),
					ContextIcon = item.State is FeedRunItemState.Removed ? "log-out" : null,
					ContextTooltip = item.State is FeedRunItemState.Removed ? "Removed once it had been absent from enough runs" : null,
				}),
		];
	}

	private static ChangeKind Kind(FeedRunItemState state)
	{
		return state switch
		{
			FeedRunItemState.Added => ChangeKind.Added,
			FeedRunItemState.Removed => ChangeKind.Removed,
			FeedRunItemState.Unaddressable => ChangeKind.Unaddressable,
		};
	}

	internal static IReadOnlyList<ChangeItem> Items(IFeedPlan summary, IReadOnlyDictionary<MediaId, MatchedMedia> media)
	{
		return
		[
			.. summary.Delta.Drops.Where(media.ContainsKey).Select(id => Pending(id, media[id], ChangeKind.Removed)),
			.. summary.Delta.Adds.Where(media.ContainsKey).Select(id => Pending(id, media[id], ChangeKind.Added)),
		];
	}

	private static ChangeItem Pending(MediaId id, MatchedMedia item, ChangeKind kind)
	{
		return new()
		{
			Title = item.Details.Title ?? string.Empty,
			Id = id,
			Media = item,
			Kind = kind,
			Subtitle = Subtitle(item),
			ContextIcon = kind is ChangeKind.Removed ? "log-out" : null,
			ContextTooltip = kind is ChangeKind.Removed ? "Absent long enough that this run will remove it" : null,
		};
	}

	internal static string Pending(IFeedPlan summary)
	{
		var parts = new List<string> { $"{summary.Verdicts.Count + summary.Unmatched.Count} fetched" };

		if (summary.Conclusiveness is not Conclusiveness.Conclusive)
		{
			parts.Add("removals held back");
		}

		return string.Join(" · ", parts);
	}

	internal static IReadOnlyList<ChangeChip> Chips(FeedRunCounts counts)
	{
		return
		[
			new ChangeChip
			{
				Icon = "list",
				Count = counts.Total,
				Tooltip = "Everything the source returned",
			},
			.. Did(counts),
			new ChangeChip
			{
				Icon = "circle-alert",
				Count = counts.Unmatched,
				Tooltip = UnmatchedTooltip(counts),
			},
		];
	}

	internal static IReadOnlyList<ChangeChip> Did(FeedRunCounts counts)
	{
		return
		[
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
		];
	}

	private static string UnmatchedSplit(FeedRunCounts counts)
	{
		List<string> parts = [$"{counts.UnmatchedNotFound} not found by any service"];
		if (counts.UnmatchedCouldNotLookUp > 0)
		{
			parts.Add($"{counts.UnmatchedCouldNotLookUp} could not be looked up (tried again next run)");
		}

		if (counts.UnmatchedConflict > 0)
		{
			parts.Add($"{counts.UnmatchedConflict} whose ids disagree with stored titles");
		}

		return string.Join(", ", parts);
	}

	private static string UnmatchedTooltip(FeedRunCounts counts)
	{
		return counts.UnmatchedCouldNotLookUp == 0 && counts.UnmatchedConflict == 0 ? "Not found by any service" : UnmatchedSplit(counts);
	}

	internal static string? Problem(FeedRun run)
	{
		if (run.Failed && run.Conclusiveness is not Conclusiveness.Stopped)
		{
			return run.SourceFailure.Match("The source could not be read, so nothing was applied.", "The source returned nothing, so nothing was applied.");
		}
		return run.Conclusiveness switch
		{
			Conclusiveness.Conclusive or Conclusiveness.Pending => null,
			Conclusiveness.Unresolved => "Some titles couldn't be looked up, so no removals were applied.",
			Conclusiveness.Stopped => "The run stopped before it finished, so some of its changes may be missing.",
			Conclusiveness.NoItems => "The source returned nothing, so no removals were applied.",
			Conclusiveness.NoMatches => "Nothing the source returned could be matched, so no removals were applied.",
			Conclusiveness.NoneAdmitted => "Nothing the source returned was admitted, so no removals were applied.",
			Conclusiveness.StillLoading => run.SourceFailure.Match(
				"The source failed partway through, so no removals were applied.",
				"The source was still loading, so no removals were applied."
			),
		};
	}

	internal static string? Problem(IFeedPlan plan) => plan.SourceFailure.Map(_ => SourceProblem(plan)).ToNullable();

	private static string SourceProblem(IFeedPlan plan)
	{
		return plan.Verdicts.Count + plan.Unmatched.Count == 0
			? "The source could not be read, so this run would change nothing."
			: "The source failed partway through, so this run would hold back removals.";
	}

	internal static string Summary(FeedRun run)
	{
		var parts = new List<string> { run.Trigger is Scheduled ? "scheduled" : "manual" };

		if (run.Counts.Total > 0)
		{
			parts.Add($"{run.Counts.Total} fetched");
		}

		if (run.Conclusiveness is not Conclusiveness.Pending)
		{
			parts.Add($"took {run.Duration.Humanize(2, Notation.Symbols)}");
		}

		return string.Join(" · ", parts);
	}

	private static string Subtitle(MatchedMedia media) => media.Details.Year?.ToString() ?? string.Empty;
}
