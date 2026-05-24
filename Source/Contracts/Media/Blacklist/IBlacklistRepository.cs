namespace Contracts.Media.Blacklist;

public interface IBlacklistRepository
{
	Task<IReadOnlyList<BlacklistedMedia>> GetBlacklistedEntriesAsync(CancellationToken cancellationToken = default);

	Task<bool> IsBlacklistedAsync(MediaId mediaId, CancellationToken cancellationToken = default);

	Task AddAsync(MediaId mediaId, CancellationToken cancellationToken = default);

	Task RemoveAsync(MediaId mediaId, CancellationToken cancellationToken = default);
}
