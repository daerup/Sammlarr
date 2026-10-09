using Domain.Media;

namespace Domain.Wantlist.Sync;

public sealed class SinkSnapshot(IReadOnlyList<MediaId> ids, IReadOnlyList<UnmatchedMedia> unmatched)
{
	private readonly HashSet<MediaId> _ids = [.. ids];

	public IReadOnlyList<MediaId> Ids { get; } = ids;

	public IReadOnlyList<UnmatchedMedia> Unmatched { get; } = unmatched;

	public Optional<WantlistSyncSinkUnreachable> Down { get; init; } = new None();

	public bool Contains(MediaId id) => _ids.Contains(id);
}
