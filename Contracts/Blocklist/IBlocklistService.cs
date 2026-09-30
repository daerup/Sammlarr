using Domain.Blocklist;
using Domain.Media;

namespace Contracts.Blocklist;

public interface IBlocklistService
{
	IAsyncEnumerable<BlocklistedMedia> GetBlocklistItemsAsync(CancellationToken cancellationToken = default);
	Task<IReadOnlySet<MediaId>> GetBlocklistIdsAsync(CancellationToken cancellationToken = default);
	Task AddToBlocklistAsync(MediaId mediaId, CancellationToken cancellationToken = default);
	Task RemoveFromBlocklistAsync(MediaId mediaId, CancellationToken cancellationToken = default);
}
