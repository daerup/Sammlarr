using Domain.Feed;
using Domain.Media;
using Domain.Watchlist;

namespace UI.Shared.Filters;

public enum SourceKind
{
	Feed,
	Manual,
	Remote,
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
	public bool Matches(WatchlistItemProvenance provenance) =>
		Kind switch
		{
			SourceKind.Feed => provenance is FeedOrigin origin && origin.Feed.Id == Feed,
			SourceKind.Manual => provenance is ManualOrigin,
			SourceKind.Remote => provenance is RemoteOrigin,
			SourceKind.Orphaned => provenance is OrphanedOrigin,
		};
}

public sealed record SourceOption(string Key, string Label, SourceChoice Choice);

public sealed record ListFilter
{
	public static readonly ListFilter None = new();

	public SourceChoice? Source { get; init; }
	public MediaType? Type { get; init; }
	public ListSort Sort { get; init; } = ListSort.RecentlyAdded;

	public int ActiveCount => (Source is null ? 0 : 1) + (Type is null ? 0 : 1);

	public bool HasFilters => ActiveCount > 0;

	public bool IsDefault => !HasFilters && Sort is ListSort.RecentlyAdded;

	public ListFilter ClearFilters() => new() { Sort = Sort };

	public bool MatchesType(MediaType type) => Type is null || Type == type;

	public bool MatchesSource(WatchlistItemProvenance provenance) => Source is not { } source || source.Matches(provenance);

	public IEnumerable<T> SortBy<T>(IEnumerable<T> items, Func<T, IdentifiedMedia> media, Func<T, DateTimeOffset> addedAt) =>
		Sort switch
		{
			ListSort.RecentlyAdded => items.OrderByDescending(addedAt),
			ListSort.OldestAdded => items.OrderBy(addedAt),
			ListSort.TitleAscending => items.OrderBy(item => media(item).Title, StringComparer.CurrentCultureIgnoreCase),
			ListSort.TitleDescending => items.OrderByDescending(item => media(item).Title, StringComparer.CurrentCultureIgnoreCase),
			ListSort.NewestRelease => items.OrderByDescending(item => media(item).ReleaseDate),
			ListSort.OldestRelease => items.OrderBy(item => media(item).ReleaseDate),
		};
}
