using Domain.Media;

namespace Domain.Watchlist.Sync;

public record WatchlistDelta(IReadOnlyList<MediaId> Adds, IReadOnlyList<MediaId> Removes)
{
	public IEnumerable<MediaId> All => Adds.Concat(Removes);

	public int Count => Adds.Count + Removes.Count;
}
