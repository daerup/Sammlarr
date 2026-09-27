using Domain.Feed;

namespace UI.Features.Home.Components;

public sealed record SourceShare(string Name, int Count);

internal static class LibraryView
{
	private const string Elsewhere = "Added by hand or from Plex";

	internal static IReadOnlyList<SourceShare> Sources(IReadOnlyDictionary<FeedId, int> ownedCounts, IReadOnlyDictionary<FeedId, string> feedNames, int total)
	{
		var named = ownedCounts
			.Where(owned => owned.Value > 0)
			.Select(owned => (Name: FeedLabel.Of(owned.Key, feedNames), owned.Value))
			.GroupBy(entry => entry.Name)
			.Select(group => (Name: group.Key, Count: group.Sum(entry => entry.Value)))
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
