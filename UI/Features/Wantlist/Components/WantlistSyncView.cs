using System.Globalization;
using Domain.Media;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using UI.Shared.Changes;
using UI.Shared.Media;

namespace UI.Features.Wantlist.Components;

internal static class WantlistSyncView
{
	internal static IReadOnlyList<ChangeItem> Items(
		WantlistSyncSummary dryRun,
		IReadOnlyDictionary<MediaId, MatchedMedia> changed,
		IReadOnlyDictionary<MediaId, MatchedMedia> unchanged,
		string sink
	)
	{
		return
		[
			.. dryRun
				.Changes.Where(change => changed.ContainsKey(change.MediaId))
				.Select(change => new ChangeItem
				{
					Title = changed[change.MediaId].Details.Title ?? string.Empty,
					Media = changed[change.MediaId],
					Kind = change.Action is WantlistOperationAction.Add ? ChangeKind.Added : ChangeKind.Removed,
					Subtitle = Subtitle(changed[change.MediaId]),
					ContextIcon = change.Direction is WantlistSyncDirection.Pull ? "arrow-down-left" : "arrow-up-right",
					ContextTooltip = change.Direction is WantlistSyncDirection.Pull ? $"Pulled from {sink}" : $"Pushed to {sink}",
				}),
			.. dryRun
				.NoOps.Where(unchanged.ContainsKey)
				.Select(id => new ChangeItem
				{
					Title = unchanged[id].Details.Title ?? string.Empty,
					Media = unchanged[id],
					Kind = ChangeKind.Unchanged,
					Subtitle = Subtitle(unchanged[id]),
				}),
			.. dryRun.Unmatched.Select(unmatched => new ChangeItem
			{
				Title = UnmatchedTitle(unmatched),
				Kind = ChangeKind.Unmatched,
				Subtitle = UnmatchedSubtitle(unmatched),
				ContextIcon = "circle-alert",
				ContextTooltip = "Sammlarr could not identify this title",
			}),
		];
	}

	private static string UnmatchedTitle(UnmatchedMedia unmatched)
	{
		return unmatched.Details.Title ?? unmatched.Externals.FirstOrDefault()?.Value ?? "Unknown title";
	}

	private static string UnmatchedSubtitle(UnmatchedMedia unmatched)
	{
		var source = unmatched.Externals.FirstOrDefault()?.Source.GetDescription();
		var year = unmatched.Details.Year?.ToString(CultureInfo.CurrentCulture);
		return string.Join(" · ", new[] { year, source is null ? null : $"on {source}", "Cannot be imported" }.Where(part => part is not null));
	}

	internal static IReadOnlyList<ChangeChip> Chips(WantlistSyncSummary dryRun) =>
		[
			new ChangeChip
			{
				Icon = "list",
				Count = dryRun.TotalChanges,
				Tooltip = "Everything that will change",
			},
			new ChangeChip
			{
				Glyph = "+",
				Count = dryRun.AddCount,
				Tooltip = "Adding",
				Tone = ChipTone.Added,
				Filters = ChangeKind.Added,
			},
			new ChangeChip
			{
				Glyph = "−",
				Count = dryRun.RemoveCount,
				Tooltip = "Removing",
				Tone = ChipTone.Removed,
				Filters = ChangeKind.Removed,
			},
			new ChangeChip
			{
				Icon = "bookmark-check",
				Count = dryRun.NoOps.Count,
				Tooltip = "Already in sync, nothing to do",
				Filters = ChangeKind.Unchanged,
			},
			new ChangeChip
			{
				Icon = "circle-alert",
				Count = dryRun.Unmatched.Count,
				Tooltip = "Could not be matched, and will never be imported",
				Tone = ChipTone.Unmatched,
				Filters = ChangeKind.Unmatched,
			},
		];

	private static string Subtitle(MatchedMedia media) => media.Details.Year?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
}
