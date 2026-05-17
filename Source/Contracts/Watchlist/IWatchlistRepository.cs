using Contracts.Media;

namespace Contracts.Watchlist;

public interface IWatchlistRepository
{
	Task<IReadOnlyList<WatchlistEntry>> GetEntriesAsync(MediaProvider mediaProvider, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<IMediaId>> GetIdsAsync(MediaProvider mediaProvider, CancellationToken cancellationToken = default);

	Task AddAsync(IEnumerable<IMediaId> mediaIds, WatchlistItemProvenance provenance, CancellationToken cancellationToken = default);

	Task RemoveAsync(IEnumerable<IMediaId> mediaIds, CancellationToken cancellationToken = default);
}
