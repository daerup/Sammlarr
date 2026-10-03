using Domain.Media;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using UI.Shared.Changes;
using UI.Shared.Media;

namespace UI.Features.Wantlist.Components;

internal static class WantlistSyncView
{
	internal static IReadOnlyList<ChangeItem> Items(WantlistSyncSummary dryRun, IReadOnlyDictionary<MediaId, IdentifiedMedia> media)
	{
		return
		[
			.. dryRun
				.Changes.Where(change => media.ContainsKey(change.MediaId))
				.Select(change => new ChangeItem
				{
					Title = media[change.MediaId].Details.Title ?? string.Empty,
					Media = media[change.MediaId],
					Kind = change.Action is WantlistOperationAction.Add ? ChangeKind.Added : ChangeKind.Removed,
					Subtitle = Subtitle(media[change.MediaId]),
					ContextIcon = change.Direction is WantlistSyncDirection.Pull ? "arrow-down-left" : "arrow-up-right",
					ContextTooltip = change.Direction is WantlistSyncDirection.Pull ? "Pulled from Plex" : "Pushed to Plex",
				}),
			.. dryRun
				.NoOps.Where(media.ContainsKey)
				.Select(id => new ChangeItem
				{
					Title = media[id].Details.Title ?? string.Empty,
					Media = media[id],
					Kind = ChangeKind.Unchanged,
					Subtitle = Subtitle(media[id]),
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
		return unmatched.Details.Title.NullIfBlank() ?? unmatched.Externals.FirstOrDefault()?.Value ?? "Unknown title";
	}

	private static string UnmatchedSubtitle(UnmatchedMedia unmatched)
	{
		var source = unmatched.Externals.FirstOrDefault()?.Source.GetDescription();
		var year = unmatched.Details.Year?.ToString();
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

	private static string Subtitle(IdentifiedMedia media) => media.Details.Year?.ToString() ?? string.Empty;
}
