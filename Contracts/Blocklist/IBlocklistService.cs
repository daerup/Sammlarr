using Domain.Blocklist;
using Domain.Media;

namespace Contracts.Blocklist;

public interface IBlocklistService
{
	Task<BlocklistSnapshot> GetAsync(CancellationToken cancellationToken = default);

	Task AddAsync(MediaId id, CancellationToken cancellationToken = default);

	Task RemoveAsync(MediaId id, CancellationToken cancellationToken = default);
}
