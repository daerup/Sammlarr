namespace Contracts.Media.Blacklist;

public interface IBlacklistRepository
{
	Task<IReadOnlyList<BlacklistedMedia>> GetBlacklistedEntriesAsync(MediaProvider mediaProvider, CancellationToken cancellationToken = default);

	Task<bool> IsBlacklistedAsync(IMediaId mediaId, CancellationToken cancellationToken = default);

	Task AddAsync(IMediaId mediaId, CancellationToken cancellationToken = default);

	Task RemoveAsync(IMediaId mediaId, CancellationToken cancellationToken = default);
}
