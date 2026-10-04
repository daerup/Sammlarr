using Domain.Blocklist;
using Domain.Media;

namespace Contracts.Blocklist;

public interface IBlocklistRepository
{
	Task<IReadOnlyList<BlocklistItem>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> AddAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> RemoveAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);
}
