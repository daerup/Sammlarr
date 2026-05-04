using Contracts;
using Contracts.Media;
using Contracts.Media.Blacklist;
using Contracts.Media.Metadata;
using Contracts.Settings;
using Shared;

namespace Core.Blacklist;

internal class BlacklistService(IBlacklistRepository repository, IProviderConfigRepository providerConfigRepository, IMediaMetadataProvider metadataProvider)
	: IBlacklistService
{
	public async Task<IReadOnlyList<BlacklistedItem>> GetBlacklistItems(CancellationToken cancellationToken = default)
	{
		var config = await providerConfigRepository.GetAsync(cancellationToken);
		var entries = await repository.GetBlacklistedEntriesAsync(config.WatchlistProvider.AsMediaProvider(), cancellationToken);
		return await entries
			.ToAsyncEnumerable()
			.SelectAwait(async e => new BlacklistedItem(await metadataProvider.GetAsync(e.Id, cancellationToken), e.AddedDate))
			.ToListAsync(cancellationToken);
	}

	public Task AddToBacklistAsync(IMediaId mediaId, CancellationToken cancellationToken = default) => repository.AddAsync(mediaId, cancellationToken);

	public Task RemoveFromBacklistAsync(IMediaId mediaId, CancellationToken cancellationToken = default) => repository.RemoveAsync(mediaId, cancellationToken);
}
