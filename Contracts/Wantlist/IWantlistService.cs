using Domain.Feed;
using Domain.Media;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;

namespace Contracts.Wantlist;

public interface IWantlistService
{
	Task<WantlistSnapshot> GetAsync(CancellationToken cancellationToken = default);

	Task<OutboxSnapshot> GetOutboxAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> AddAsync(IEnumerable<MediaId> ids, WantlistItemProvenance provenance, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> RemoveAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> RemoveAsync(IEnumerable<MediaId> ids, FeedId addedBy, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> RequeueAllAsync(CancellationToken cancellationToken = default);
}
