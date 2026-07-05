using Contracts;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Settings;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;

namespace Core.Media;

internal sealed class MediaMetadataResolver(
	IKeyedServiceIndex<MediaSearchProvider, IMediaSearchProvider> searchIndex,
	IKeyedServiceIndex<MediaProvider, IMetadataEnricher> enricherIndex,
	IKeyedServiceIndex<RatingProvider, IMediaRatingService> ratingIndex,
	IProviderConfigRepository configRepository
)
{
	private static readonly MediaProvider[] EnricherChain = [MediaProvider.Plex, MediaProvider.Tmdb];

	public async Task<Result<MediaLookup>> ResolveAsync(MediaIdentity identity, MediaResolutionBuffer buffer, CancellationToken cancellationToken = default)
	{
		var config = await configRepository.GetAsync(cancellationToken);
		var detail = await LookUpAsync(identity, config.SearchProvider, cancellationToken);
		var identified = detail.AsIdentified();
		if (identified is not null)
		{
			var enriched = await EnrichAsync(identified.Value, config.SearchProvider, cancellationToken);
			detail = await ApplyRatingsAsync(enriched, config.RatingProvider, cancellationToken);
		}

		buffer.Add(detail);
		return Success.Of(detail);
	}

	private async Task<MediaLookup> LookUpAsync(MediaIdentity identity, MediaSearchProvider provider, CancellationToken cancellationToken)
	{
		var providerMedia = provider.AsMediaProvider();
		var external = identity.Externals.OfProvider(providerMedia) ?? identity.Externals.First();
		var detail = await searchIndex[provider].LookUpAsync(external, cancellationToken);
		return detail.CombineIdentity(identity);
	}

	private async Task<IdentifiedMedia> ApplyRatingsAsync(IdentifiedMedia details, RatingProvider ratingProvider, CancellationToken cancellationToken)
	{
		if (details.Rating.Sources.Any(rating => rating.Source == ratingProvider))
		{
			return details;
		}

		var ratingId = details.Externals.OfProvider(ratingProvider.AsMediaProvider());
		if (ratingId is null)
		{
			return details;
		}

		var ratings = await ratingIndex[ratingProvider].GetRatingsAsync(ratingId, cancellationToken);
		return MediaMerger.WithRatings(details, ratings);
	}

	private async Task<IdentifiedMedia> EnrichAsync(IdentifiedMedia details, MediaSearchProvider searchProvider, CancellationToken cancellationToken)
	{
		var primaryProvider = searchProvider.AsMediaProvider();
		foreach (var provider in EnricherChain.Where(p => p != primaryProvider))
		{
			if (details is not IPartialMedia partial)
			{
				break;
			}

			var providerId = details.Externals.OfProvider(provider);
			if (providerId is null)
			{
				continue;
			}

			var enrichment = await enricherIndex[provider].LookUpAsync(providerId, cancellationToken);
			var enriched = enrichment?.AsIdentified();
			if (enriched is null)
			{
				continue;
			}
			details = enriched.Value switch
			{
				IMedia complete => partial.MergeWith(complete),
				IPartialMedia other => partial.MergeWith(other),
			};
		}
		return details;
	}
}
