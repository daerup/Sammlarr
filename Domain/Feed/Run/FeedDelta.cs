using Domain.Media;

namespace Domain.Feed.Run;

public record FeedDelta(IReadOnlyList<MediaId> Adds, IReadOnlyList<MediaId> Keeps, IReadOnlyList<MediaId> Drops, IReadOnlyList<MediaId> Waives)
{
	public static FeedDelta None => new([], [], [], []);

	public bool HasChanges => Adds.Count + Drops.Count + Waives.Count > 0;
}
