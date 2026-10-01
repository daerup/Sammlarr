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
	IProviderConfigService providerConfig
)
{
	private static readonly MediaProvider[] EnricherChain = [MediaProvider.Plex, MediaProvider.Tmdb];

	public async Task<Result<MediaLookup>> ResolveAsync(MediaIdentity identity, MediaResolutionBuffer buffer, CancellationToken cancellationToken = default)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
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
		var enriched = await EnrichAsync(identified.Value, searchProvider, ratingProvider, cancellationToken);
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
		if (HasRatingFrom(details.Rating, ratingProvider))
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

	private async Task<IdentifiedMedia> EnrichAsync(
		IdentifiedMedia details,
		SearchProvider searchProvider,
		RatingProvider ratingProvider,
		CancellationToken cancellationToken
	)
	{
		var primaryProvider = searchProvider.AsMediaProvider();
		var unanswered = MediaField.None;
		foreach (var provider in EnricherChain.Where(p => p != primaryProvider))
		{
			if (details is not IPartialMedia partial)
			{
				break;
			}

			var enricher = enricherIndex[provider];
			var fills = provider == ratingProvider.AsMediaProvider() ? enricher.Fills : enricher.Fills & ~MediaField.Rating;
			if (!Needed(partial, ratingProvider).Overlaps(fills) || partial.Identity.Externals.OfProvider(provider) is not { } id)
			{
				continue;
			}

			switch (await enricher.LookUpAsync(id, cancellationToken))
			{
				case Success<MediaLookup>(var lookup) when lookup.ToIdentified() is Some<IdentifiedMedia>(var enriched):
					details = partial.MergeWith(enriched);
					break;
				case Error { Exception: ProviderUnavailableException }:
					unanswered |= fills;
					break;
			}
		}
		return details is IPartialMedia remaining ? remaining.Confirming(remaining.Missing & ~unanswered) : details;
	}

	private static MediaField Needed(IPartialMedia partial, RatingProvider ratingProvider) =>
		HasRatingFrom(partial.Rating, ratingProvider) ? partial.Missing : partial.Missing | MediaField.Rating;

	private static bool HasRatingFrom(RatingSummary rating, RatingProvider provider) => rating.Sources.Any(source => source.Source == provider);
}
