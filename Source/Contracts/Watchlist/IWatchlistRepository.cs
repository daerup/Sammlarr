using Contracts.Media;

namespace Contracts.Watchlist;

public interface IWatchlistRepository
{
	Task<IReadOnlyList<WatchlistedMedia>> GetEntriesAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> GetIdsAsync(CancellationToken cancellationToken = default);

	Task AddAsync(IEnumerable<MediaId> mediaIds, WatchlistItemProvenance provenance, CancellationToken cancellationToken = default);

	Task RemoveAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);
}
