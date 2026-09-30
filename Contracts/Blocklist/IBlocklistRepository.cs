using Domain.Blocklist;
using Domain.Media;

namespace Contracts.Blocklist;

public interface IBlocklistRepository
{
	Task<IReadOnlyList<BlocklistEntry>> GetBlocklistedEntriesAsync(CancellationToken cancellationToken = default);

	Task AddAsync(MediaId mediaId, CancellationToken cancellationToken = default);

	Task RemoveAsync(MediaId mediaId, CancellationToken cancellationToken = default);
}
