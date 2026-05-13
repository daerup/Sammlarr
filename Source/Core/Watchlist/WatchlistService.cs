using System.Runtime.CompilerServices;
using Contracts.Auth;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Settings;
using Contracts.Watchlist;
using Shared;
using Shared.Telemetry;

namespace Core.Watchlist;

internal class WatchlistService(
	IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider> providerIndex,
	IProviderConfigRepository configRepository,
	IMediaMetadataProvider metadataProvider
) : IWatchlistService
{
	public async Task<WatchlistPageResult> GetWatchlistAsync(int page, CancellationToken cancellationToken = default)
	{
		var (providerKey, provider) = await ResolveProviderAsync(cancellationToken);
		using var activity = SammlarrTelemetry.StartWatchlistLoad(providerKey, page);
		var providerPage = await provider.GetWatchlistAsync(page, cancellationToken);

		var enriched = new List<IMediaDetailResult>(providerPage.Items.Count);
		await foreach (
			var detail in providerPage
				.Items.ToAsyncEnumerable()
				.SelectAwait(
					async id => await metadataProvider.GetAsync(id, cancellationToken),
					IMediaMetadataProvider.RecommendedConcurrency,
					cancellationToken
				)
		)
		{
			enriched.Add(detail);
		}
		return new WatchlistPageResult(enriched, providerPage.TotalPages);
	}

	public async IAsyncEnumerable<IMediaDetailResult> GetWatchlistAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var (providerKey, provider) = await ResolveProviderAsync(cancellationToken);
		var page = 1;
		int totalPages;
		do
		{
			using var activity = SammlarrTelemetry.StartWatchlistLoad(providerKey, page);
			var watchlistContext = activity?.Context ?? default;
			var providerPage = await provider.GetWatchlistAsync(page, cancellationToken);
			totalPages = providerPage.TotalPages;
			var pipeline = providerPage
				.Items.ToAsyncEnumerable()
				.SelectAwait(
					async id => await metadataProvider.GetAsync(id, cancellationToken, watchlistContext),
					IMediaMetadataProvider.RecommendedConcurrency,
					cancellationToken
				);

			await foreach (var detail in pipeline)
			{
				yield return detail;
			}
			page++;
		} while (page <= totalPages);
	}

	public async Task<bool> AddToWatchlistAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var (_, provider) = await ResolveProviderAsync(cancellationToken);
		return await provider.AddToWatchlistAsync(mediaId, cancellationToken);
	}

	public async Task<bool> RemoveFromWatchlistAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var (_, provider) = await ResolveProviderAsync(cancellationToken);
		return await provider.RemoveFromWatchlistAsync(mediaId, cancellationToken);
	}

	private async Task<(WatchlistProvider Key, IWatchlistProvider Provider)> ResolveProviderAsync(CancellationToken cancellationToken)
	{
		var config = await configRepository.GetAsync(cancellationToken);
		return (config.WatchlistProvider, providerIndex[config.WatchlistProvider]);
	}
}
