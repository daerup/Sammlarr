using Contracts.Media;
using Domain.Blacklist;
using Domain.Media;

namespace Contracts.Blacklist;

public interface IBlacklistRepository
{
	Task<IReadOnlyList<BlacklistEntry>> GetBlacklistedEntriesAsync(CancellationToken cancellationToken = default);

	Task AddAsync(MediaId mediaId, CancellationToken cancellationToken = default);

	Task RemoveAsync(MediaId mediaId, CancellationToken cancellationToken = default);
}
