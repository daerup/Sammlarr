using Contracts.Media;

namespace Contracts.Blacklist;

public interface IBlacklistService
{
	IAsyncEnumerable<BlacklistedMediaDetail> GetBlacklistItems(CancellationToken cancellationToken = default);
	Task<IReadOnlySet<MediaId>> GetBlacklistIdsAsync(CancellationToken cancellationToken = default);
	Task AddToBacklistAsync(MediaId mediaId, CancellationToken cancellationToken = default);
	Task RemoveFromBacklistAsync(MediaId mediaId, CancellationToken cancellationToken = default);
}
