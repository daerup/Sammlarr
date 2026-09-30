using Domain.Feed;
using Domain.Media;
using Domain.Wantlist;

namespace Contracts.Wantlist;

public interface IWantlistService
{
	IAsyncEnumerable<WantlistedMedia> GetWantlistItemsAsync(WantlistQueryOptions options, CancellationToken cancellationToken = default);

	Task<IReadOnlySet<MediaId>> GetWantlistIdsAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<FeedId, int>> GetOwnedCountsAsync(CancellationToken cancellationToken = default);

	Task<Optional<WantlistItem>> GetEntryAsync(MediaId mediaId, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<MediaId, WantlistItem>> GetEntriesAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> AddToWantlistAsync(
		IEnumerable<MediaId> mediaIds,
		WantlistItemProvenance provenance,
		CancellationToken cancellationToken = default
	);

	Task RemoveFromWantlistAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> RemoveOwnedAsync(IEnumerable<MediaId> mediaIds, FeedId owner, CancellationToken cancellationToken = default);
}
