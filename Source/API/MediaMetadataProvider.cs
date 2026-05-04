using Contracts;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Settings;

namespace API;

internal class MediaMetadataProvider(
	IMediaMetadataRepository repository,
	IKeyedServiceIndex<MediaSearchProvider, IMediaSearchService> searchIndex,
	IKeyedServiceIndex<MediaProvider, IMetadataEnricher> enricherIndex,
	IProviderConfigRepository configRepository,
	IDateTimeProvider dateTimeProvider
) : IMediaMetadataProvider
{
	private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(7);
	private static readonly MediaProvider[] EnricherChain = [MediaProvider.Plex, MediaProvider.Tmdb];

	public async Task<IMediaDetailResult> GetAsync(IMediaId anchor, CancellationToken cancellationToken = default)
	{
		var cached = await repository.GetByMediaIdAsync(anchor, cancellationToken);
		var now = dateTimeProvider.Now;

		if (cached is not null && now - cached.FetchedAt < StaleAfter)
		{
			return cached.Details;
		}

		var config = await configRepository.GetAsync(cancellationToken);
		var search = searchIndex[config.SearchProvider];
		var details = await search.LookUpAsync(anchor, cancellationToken);

		var primaryProvider = config.SearchProvider.AsMediaProvider();
		details = await EnrichAsync(details, primaryProvider, cancellationToken);

		await repository.UpsertAsync(anchor, details, cancellationToken);
		return details;
	}

	public Task ReportStaleAsync(IMediaId anchor, CancellationToken cancellationToken = default) => repository.RemoveAsync(anchor, cancellationToken);

	private async Task<IMediaDetailResult> EnrichAsync(IMediaDetailResult details, MediaProvider primaryProvider, CancellationToken cancellationToken)
	{
		foreach (var providerKey in EnricherChain)
		{
			if (providerKey == primaryProvider)
			{
				continue;
			}
			if (MetadataCompleteness.IsComplete(details))
			{
				break;
			}

			var idForProvider = details.AdditionalIds.FirstOrDefault(id => id.Source == providerKey);
			if (idForProvider is null)
			{
				continue;
			}

			var enricher = enricherIndex[providerKey];
			var enrichment = await enricher.LookUpAsync(idForProvider, cancellationToken);
			if (enrichment is null)
			{
				continue;
			}

			details = MetadataMerger.FillMissing(details, enrichment);
		}

		return details;
	}
}
