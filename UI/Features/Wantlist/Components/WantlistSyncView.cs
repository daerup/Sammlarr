using Domain.Media;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using UI.Shared.Changes;

namespace UI.Features.Wantlist.Components;

internal static class WantlistSyncView
{
	internal static IReadOnlyList<ChangeEntry> Entries(WantlistSyncSummary dryRun, IReadOnlyDictionary<MediaId, MediaLookup> media) =>
		[
			.. dryRun
				.Changes.Where(change => media.ContainsKey(change.MediaId))
				.Select(change => new ChangeEntry
				{
					Title = media[change.MediaId] is IdentifiedMedia named ? named.Title : string.Empty,
					Media = media[change.MediaId],
					Kind = change.Action is WantlistOperationAction.Add ? ChangeKind.Added : ChangeKind.Removed,
					Subtitle = Subtitle(media[change.MediaId]),
					ContextIcon = change.Direction is WantlistSyncDirection.Pull ? "arrow-down-left" : "arrow-up-right",
					ContextTooltip = change.Direction is WantlistSyncDirection.Pull ? "Pulled from Plex" : "Pushed to Plex",
				}),
			.. dryRun
				.NoOps.Where(media.ContainsKey)
				.Select(id => new ChangeEntry
				{
					Title = media[id] is IdentifiedMedia named ? named.Title : string.Empty,
					Media = media[id],
					Kind = ChangeKind.Unchanged,
					Subtitle = Subtitle(media[id]),
				}),
			.. dryRun.Unmatched.Select(unmatched => new ChangeEntry
			{
				Title = UnmatchedTitle(unmatched),
				Kind = ChangeKind.Unmatched,
				Subtitle = UnmatchedSubtitle(unmatched),
				ContextIcon = "circle-alert",
				ContextTooltip = "Sammlarr could not identify this title",
			}),
		];

	private static string UnmatchedTitle(UnmatchedMedia unmatched) =>
		unmatched.Title.NullIfBlank() ?? unmatched.Externals.FirstOrDefault()?.Value ?? "Unknown title";

	private static string UnmatchedSubtitle(UnmatchedMedia unmatched)
	{
		var source = unmatched.Externals.FirstOrDefault()?.Source.GetDescription();
		var year = unmatched.ReleaseDate?.Year.ToString();
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

	private static string Subtitle(MediaLookup media) =>
		media is IdentifiedMedia identified && identified.ReleaseDate is { } released ? released.Year.ToString() : string.Empty;
}
