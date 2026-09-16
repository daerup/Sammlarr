using Domain.Blacklist;
using Domain.Media;

namespace Contracts.Blacklist;

public interface IBlacklistService
{
	IAsyncEnumerable<BlacklistedMedia> GetBlacklistItemsAsync(CancellationToken cancellationToken = default);
	Task<IReadOnlySet<MediaId>> GetBlacklistIdsAsync(CancellationToken cancellationToken = default);
	Task AddToBacklistAsync(MediaId mediaId, CancellationToken cancellationToken = default);
	Task RemoveFromBacklistAsync(MediaId mediaId, CancellationToken cancellationToken = default);
}
