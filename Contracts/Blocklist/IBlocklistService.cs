using Domain.Blocklist;
using Domain.Media;

namespace Contracts.Blocklist;

public interface IBlocklistService
{
	Task<BlocklistSnapshot> GetAsync(CancellationToken cancellationToken = default);

	Task AddAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	Task RemoveAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);
}
