namespace Shared.Options;

public record FeedFilterOptions : IOptionsSection
{
	public IReadOnlyList<FilterLevelStyle> Levels { get; set; } = [];

	public int MaxNesting => Levels.Count - 1;

	public static string SectionName => "Sammlarr:FeedFilters";
}
