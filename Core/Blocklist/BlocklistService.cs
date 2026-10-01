using Contracts.Blocklist;
using Domain.Blocklist;
using Domain.Media;

namespace Core.Blocklist;

internal sealed class BlocklistService(IBlocklistRepository repository) : IBlocklistService
{
	public async Task<BlocklistSnapshot> GetAsync(CancellationToken cancellationToken = default) => new(await repository.GetAllAsync(cancellationToken));

	public Task AddAsync(MediaId id, CancellationToken cancellationToken = default) => repository.AddAsync(id, cancellationToken);

	public Task RemoveAsync(MediaId id, CancellationToken cancellationToken = default) => repository.RemoveAsync(id, cancellationToken);
}
