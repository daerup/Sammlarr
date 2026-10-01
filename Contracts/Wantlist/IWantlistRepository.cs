using Domain.Feed;
using Domain.Media;
using Domain.Wantlist;

namespace Contracts.Wantlist;

public interface IWantlistRepository
{
	Task<IReadOnlyList<WantlistItem>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<FeedId, int>> CountByFeedAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> AddAsync(IEnumerable<MediaId> ids, WantlistItemProvenance provenance, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> RemoveAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> RemoveAsync(IEnumerable<MediaId> ids, FeedId addedBy, CancellationToken cancellationToken = default);

	Task OrphanAsync(Domain.Feed.Feed addedBy, CancellationToken cancellationToken = default);
}
