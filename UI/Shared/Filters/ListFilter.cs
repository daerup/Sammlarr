using Domain.Feed;
using Domain.Media;
using Domain.Wantlist;

namespace UI.Shared.Filters;

public enum SourceKind
{
	Feed,
	Manual,
	Sink,
	Orphaned,
}

public enum ListSort
{
	RecentlyAdded,
	OldestAdded,
	TitleAscending,
	TitleDescending,
	NewestRelease,
	OldestRelease,
}

public readonly record struct SourceChoice(SourceKind Kind, FeedId? Feed = null)
{
	public bool Matches(WantlistItemProvenance provenance) =>
		Kind switch
		{
			SourceKind.Feed => provenance is FeedOrigin origin && origin.Feed.Id == Feed,
			SourceKind.Manual => provenance is ManualOrigin,
			SourceKind.Sink => provenance is SinkOrigin,
			SourceKind.Orphaned => provenance is OrphanedOrigin,
		};
}

public sealed record SourceOption(string Key, string Label, SourceChoice Choice);

public sealed record StatusOption(string Key, string Label);

public sealed record ListFilter
{
	public static readonly ListFilter None = new();

	public IReadOnlyList<SourceChoice> Sources { get; init; } = [];
	public MediaType? Type { get; init; }
	public string? Search { get; init; }
	public IReadOnlyList<string> Statuses { get; init; } = [];
	public ListSort Sort { get; init; } = ListSort.RecentlyAdded;

	public int ActiveCount => (Sources.Count > 0 ? 1 : 0) + (Type is null ? 0 : 1) + (string.IsNullOrWhiteSpace(Search) ? 0 : 1) + (Statuses.Count > 0 ? 1 : 0);

	public bool HasStatus(string key) => Statuses.Contains(key);

	public bool HasFilters => ActiveCount > 0;

	public bool IsDefault => !HasFilters && Sort is ListSort.RecentlyAdded;

	public ListFilter ClearFilters() => new() { Sort = Sort };

	public bool MatchesType(MediaType type) => Type is null || Type == type;

	public bool MatchesSearch(string? title) =>
		string.IsNullOrWhiteSpace(Search) || (title is not null && title.Contains(Search.Trim(), StringComparison.CurrentCultureIgnoreCase));

	public bool MatchesSource(WantlistItemProvenance provenance) => Sources.Count == 0 || Sources.Any(source => source.Matches(provenance));

	public IEnumerable<T> SortBy<T>(IEnumerable<T> items, Func<T, IdentifiedMedia> media, Func<T, DateTimeOffset> addedAt)
	{
		return Sort switch
		{
			ListSort.RecentlyAdded => items.OrderByDescending(addedAt),
			ListSort.OldestAdded => items.OrderBy(addedAt),
			ListSort.TitleAscending => items.OrderBy(item => media(item).Details.Title, StringComparer.CurrentCultureIgnoreCase),
			ListSort.TitleDescending => items.OrderByDescending(item => media(item).Details.Title, StringComparer.CurrentCultureIgnoreCase),
			ListSort.NewestRelease => items.OrderByDescending(item => media(item).Details.ReleaseDate),
			ListSort.OldestRelease => items.OrderBy(item => media(item).Details.ReleaseDate),
		};
	}
}
