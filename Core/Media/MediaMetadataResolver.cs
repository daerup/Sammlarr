using Contracts.DI;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Settings;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;

namespace Core.Media;

internal sealed class MediaMetadataResolver(
	IKeyedServiceIndex<SearchProvider, IMediaSearchProvider> searchIndex,
	IKeyedServiceIndex<MediaProvider, IMetadataEnricher> enricherIndex,
	IKeyedServiceIndex<RatingProvider, IMediaRatingService> ratingIndex,
	IProviderConfigRepository configRepository
)
{
	private static readonly MediaProvider[] EnricherChain = [MediaProvider.Plex, MediaProvider.Tmdb];

	public async Task<Result<MediaLookup>> ResolveAsync(MediaIdentity identity, MediaResolutionBuffer buffer, CancellationToken cancellationToken = default)
	{
		var config = await configRepository.GetAsync(cancellationToken);
		return await LookUpAsync(identity, config.SearchProvider, cancellationToken)
			.Map(detail => CompleteAsync(detail, config.SearchProvider, config.RatingProvider, cancellationToken))
			.Apply(buffer.Add);
	}

	private async Task<MediaLookup> CompleteAsync(
		MediaLookup detail,
		SearchProvider searchProvider,
		RatingProvider ratingProvider,
		CancellationToken cancellationToken
	)
	{
		var identified = detail.AsIdentified();
		if (identified is null)
		{
			return detail;
		}
		var enriched = await EnrichAsync(identified.Value, searchProvider, cancellationToken);
		return await ApplyRatingsAsync(enriched, ratingProvider, cancellationToken);
	}

	private Task<Result<MediaLookup>> LookUpAsync(MediaIdentity identity, SearchProvider provider, CancellationToken cancellationToken)
	{
		var providerMedia = provider.AsMediaProvider();
		var external = identity.Externals.OfProvider(providerMedia) ?? identity.Externals.First();
		return searchIndex[provider].LookUpAsync(external, cancellationToken).Map(detail => detail.CombineIdentity(identity));
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

		return await ratingIndex[ratingProvider]
			.GetRatingsAsync(ratingId, cancellationToken)
			.Map(ratings => MediaMerger.WithRatings(details, ratings))
			.GetValueOr(details);
	}

	private async Task<IdentifiedMedia> EnrichAsync(IdentifiedMedia details, SearchProvider searchProvider, CancellationToken cancellationToken)
	{
		var primaryProvider = searchProvider.AsMediaProvider();
		foreach (var provider in EnricherChain.Where(p => p != primaryProvider))
		{
			if (details is not IPartialMedia partial)
			{
				break;
			}

			details = await EnrichFromAsync(partial, provider, cancellationToken).Map(enriched => partial.MergeWith(enriched)).GetValueOr(details);
		}
		return details;
	}

	private Task<Optional<IdentifiedMedia>> EnrichFromAsync(IPartialMedia partial, MediaProvider provider, CancellationToken cancellationToken) =>
		Optional
			.Of(partial.Identity.Externals.OfProvider(provider))
			.Map(id => enricherIndex[provider].LookUpAsync(id, cancellationToken).ToOptional())
			.Map(lookup => lookup.ToIdentified());
}
