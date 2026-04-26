namespace Contracts.Media.Blacklist;

public interface IBlacklistService
{
	Task<IReadOnlyList<IBlacklistItem>> GetBlacklistItems(CancellationToken cancellationToken = default);

	Task<bool> IsBlacklistedAsync(IMediaId mediaId, CancellationToken cancellationToken = default);

	Task AddToBacklistAsync(IMediaId mediaId, CancellationToken cancellationToken = default);

	Task RemoveFromBacklistAsync(IMediaId mediaId, CancellationToken cancellationToken = default);
}
