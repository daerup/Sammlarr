using Contracts;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Blacklist;
using Contracts.Media.Search;
using Contracts.Settings;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace Core.Blacklist;

internal class BlacklistService(
	IBlacklistRepository repository,
	IProviderConfigRepository providerConfigRepository,
	IKeyedServiceIndex<MediaSearchProvider, IMediaSearchService> searchServiceIndex
) : IBlacklistService
{
	public async Task<IReadOnlyList<IBlacklistItem>> GetBlacklistItems(CancellationToken cancellationToken = default)
	{
		var config = await providerConfigRepository.GetAsync(cancellationToken);
		var searchService = searchServiceIndex[config.SearchProvider];

		var entries = await repository.GetBlacklistedEntriesAsync(config.WatchlistProvider.AsMediaProvider(), cancellationToken);
		return await entries
			.ToAsyncEnumerable()
			.SelectAwait(async e => BlacklistItem.From(await searchService.LookUpAsync(e.Id, cancellationToken), e.AddedDate))
			.ToListAsync(cancellationToken: cancellationToken);
	}

	public Task<bool> IsBlacklistedAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		return repository.IsBlacklistedAsync(mediaId, cancellationToken);
	}

	public Task AddToBacklistAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		return repository.AddAsync(mediaId, cancellationToken);
	}

	public Task RemoveFromBacklistAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		return repository.RemoveAsync(mediaId, cancellationToken);
	}
}
