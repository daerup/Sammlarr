using Contracts.Blocklist;
using Contracts.Identity;
using Core.Announcing;
using Domain.Blocklist;
using Domain.Media;

namespace Core.Blocklist;

internal sealed class BlocklistService(IBlocklistRepository repository, Announcer announcer, ICurrentUser currentUser) : IBlocklistService
{
	public async Task<BlocklistSnapshot> GetAsync(CancellationToken cancellationToken = default) => new(await repository.GetAllAsync(cancellationToken));

	public async Task AddAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		var added = await repository.AddAsync(ids, cancellationToken);
		if (added.Count > 0)
		{
			announcer.Announce(new BlocklistChangedEvent(currentUser.Id, added, []));
		}
	}

	public async Task RemoveAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		var removed = await repository.RemoveAsync(ids, cancellationToken);
		if (removed.Count > 0)
		{
			announcer.Announce(new BlocklistChangedEvent(currentUser.Id, [], removed));
		}
	}
}
