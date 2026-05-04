using System.Runtime.CompilerServices;
using Contracts.Auth;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Settings;
using Contracts.Watchlist;

namespace Core.Watchlist;

internal class WatchlistService(
	IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider> providerIndex,
	IProviderConfigRepository configRepository,
	IMediaMetadataProvider metadataProvider
) : IWatchlistService
{
	public async Task<WatchlistPageResult> GetWatchlistAsync(int page, CancellationToken cancellationToken = default)
	{
		var provider = await ResolveProviderAsync(cancellationToken);
		var providerPage = await provider.GetWatchlistAsync(page, cancellationToken);

		var enriched = new List<IMediaDetailResult>(providerPage.Items.Count);
		foreach (var id in providerPage.Items)
		{
			enriched.Add(await metadataProvider.GetAsync(id, cancellationToken));
		}

		return new WatchlistPageResult(enriched, providerPage.TotalPages);
	}

	public async IAsyncEnumerable<IMediaDetailResult> GetWatchlistPagedAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var provider = await ResolveProviderAsync(cancellationToken);
		var page = 1;
		int totalPages;
		do
		{
			var providerPage = await provider.GetWatchlistAsync(page, cancellationToken);
			totalPages = providerPage.TotalPages;
			foreach (var id in providerPage.Items)
			{
				yield return await metadataProvider.GetAsync(id, cancellationToken);
			}
			page++;
		} while (page <= totalPages);
	}

	public async Task<bool> AddToWatchlistAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var provider = await ResolveProviderAsync(cancellationToken);
		return await provider.AddToWatchlistAsync(mediaId, cancellationToken);
	}

	public async Task<bool> RemoveFromWatchlistAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var provider = await ResolveProviderAsync(cancellationToken);
		return await provider.RemoveFromWatchlistAsync(mediaId, cancellationToken);
	}

	private async Task<IWatchlistProvider> ResolveProviderAsync(CancellationToken cancellationToken)
	{
		var config = await configRepository.GetAsync(cancellationToken);
		return providerIndex[config.WatchlistProvider];
	}
}
