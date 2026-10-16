using Domain.Media;

namespace Domain.Wantlist.Sync;

public record WantlistDelta(IReadOnlyList<MediaId> Adds, IReadOnlyList<MediaId> Removes)
{
	public int Count => Adds.Count + Removes.Count;
}
