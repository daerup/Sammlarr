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

		var enriched = await providerPage
			.Items.ToAsyncEnumerable()
			.SelectAwait(async id => await metadataProvider.GetAsync(id, cancellationToken), IMediaMetadataProvider.RecommendedConcurrency, cancellationToken)
			.ToListAsync(cancellationToken);
		return new WatchlistPageResult(enriched, providerPage.TotalPages);
	}

	public async IAsyncEnumerable<IMediaDetailResult> GetWatchlistAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var (providerKey, provider) = await ResolveProviderAsync(cancellationToken);
		using var loadActivity = SammlarrTelemetry.StartWatchlistLoad(providerKey);

		async ValueTask<WatchlistProviderPage> LoadPage(int page)
		{
			using var activity = SammlarrTelemetry.StartWatchlistLoad(providerKey, page);
			return await provider.GetWatchlistAsync(page, cancellationToken);
		}

		var firstPage = await LoadPage(1);
		var remainingPages = Enumerable
			.Range(2, Math.Max(0, firstPage.TotalPages - 1))
			.ToAsyncEnumerable()
			.SelectAwait(LoadPage, IWatchlistService.RecommendedConcurrency, cancellationToken);

		var ids = firstPage.Items.ToAsyncEnumerable().Concat(remainingPages.SelectMany(p => p.Items.ToAsyncEnumerable()));

		var pipeline = ids.SelectAwait(
			async id => await metadataProvider.GetAsync(id, cancellationToken),
			IMediaMetadataProvider.RecommendedConcurrency,
			cancellationToken
		);

		await foreach (var detail in pipeline)
		{
			yield return detail;
		}
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
