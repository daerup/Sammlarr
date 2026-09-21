using Domain.Media;

namespace UI.Shared.Changes;

public enum ChangeKind
{
	Removed,
	Added,
	Unchanged,
	Unmatched,
}

public enum ChipTone
{
	Neutral,
	Added,
	Removed,
	Unmatched,
}

public sealed record ChangeEntry
{
	public required string Title { get; init; }
	public MediaId? Id { get; init; }
	public MediaLookup? Media { get; init; }
	public required ChangeKind Kind { get; init; }
	public string? Subtitle { get; init; }
	public string? ContextIcon { get; init; }
	public string? ContextTooltip { get; init; }
}

public sealed record ChangeChip
{
	public string? Icon { get; init; }
	public string? Glyph { get; init; }
	public required int Count { get; init; }
	public required string Tooltip { get; init; }
	public ChipTone Tone { get; init; } = ChipTone.Neutral;
	public ChangeKind? Filters { get; init; }
	public bool StartsCountsOnly { get; init; }
}
