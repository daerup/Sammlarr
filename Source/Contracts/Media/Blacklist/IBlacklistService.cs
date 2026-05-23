namespace Contracts.Media.Blacklist;

public interface IBlacklistService
{
	IAsyncEnumerable<BlacklistedMediaDetail> GetBlacklistItems(CancellationToken cancellationToken = default);
	Task<IReadOnlySet<IMediaId>> GetBlacklistIdsAsync(CancellationToken cancellationToken = default);
	Task AddToBacklistAsync(IMediaId mediaId, CancellationToken cancellationToken = default);
	Task RemoveFromBacklistAsync(IMediaId mediaId, CancellationToken cancellationToken = default);
}
