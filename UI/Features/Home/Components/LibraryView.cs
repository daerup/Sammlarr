using Domain.Feed;

namespace UI.Features.Home.Components;

public sealed record SourceShare(string Name, int Count);

internal static class LibraryView
{
	private const string Elsewhere = "Added by hand or from Plex";

	internal static IReadOnlyList<SourceShare> Sources(IReadOnlyList<FeedOverview> feeds, int total)
	{
		var named = feeds
			.Where(overview => overview.OnWantlist > 0)
			.GroupBy(overview => overview.Feed.Name)
			.Select(group => (Name: group.Key, Count: group.Sum(overview => overview.OnWantlist)))
			.OrderByDescending(entry => entry.Count)
			.ToList();

		var elsewhere = total - named.Sum(entry => entry.Count);
		if (elsewhere > 0)
		{
			named.Add((Elsewhere, elsewhere));
		}

		return total == 0 ? [] : [.. named.Select(entry => new SourceShare(entry.Name, entry.Count))];
	}
}
