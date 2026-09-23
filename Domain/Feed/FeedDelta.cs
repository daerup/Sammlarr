using Domain.Media;

namespace Domain.Feed;

public record FeedDelta(IReadOnlyList<MediaId> Adds, IReadOnlyList<MediaId> Keeps, IReadOnlyList<MediaId> Drops)
{
	public static FeedDelta None => new([], [], []);

	public bool HasChanges => Adds.Count + Drops.Count > 0;
}
