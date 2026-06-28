using Contracts;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Settings;
using Shared.Telemetry;

namespace Core.Media;

internal sealed class MediaMetadataResolver(
	IKeyedServiceIndex<MediaSearchProvider, IMediaSearchProvider> searchIndex,
	IKeyedServiceIndex<MediaProvider, IMetadataEnricher> enricherIndex,
	IKeyedServiceIndex<RatingProvider, IMediaRatingService> ratingIndex,
	IProviderConfigRepository configRepository
)
{
	private static readonly MediaProvider[] EnricherChain = [MediaProvider.Plex, MediaProvider.Tmdb];

	public async Task<Result<MediaMatch>> ResolveAsync(MediaIdentity identity, MediaResolutionBuffer buffer, CancellationToken cancellationToken = default)
	{
		using var activity = SammlarrTelemetry.StartMetadataItem(identity.Id);

		var config = await configRepository.GetAsync(cancellationToken);
		var detail = await LookUpAsync(identity, config.SearchProvider, cancellationToken);
		if (detail is not UnmatchedMedia)
		{
			detail = await EnrichAsync(detail, config.SearchProvider, cancellationToken);
			detail = await ApplyRatingsAsync(detail, config.RatingProvider, cancellationToken);
		}

		SammlarrTelemetry.TagItemSuccess(activity, detail is not UnmatchedMedia);
		buffer.Add(detail);
		return detail;
	}

	private async Task<MediaMatch> LookUpAsync(MediaIdentity identity, MediaSearchProvider provider, CancellationToken cancellationToken)
	{
		var providerMedia = provider.AsMediaProvider();
		var external = identity.Externals.OfProvider(providerMedia) ?? identity.Externals.First();
		using var activity = SammlarrTelemetry.StartMetadataLookup(provider, external);
		var detail = await searchIndex[provider].LookUpAsync(external, cancellationToken);
		return detail.CombineIdentity(identity);
	}

	private async Task<MediaMatch> ApplyRatingsAsync(MediaMatch details, RatingProvider ratingProvider, CancellationToken cancellationToken)
	{
		var ratingId = details.Externals.OfProvider(ratingProvider.AsMediaProvider());
		if (ratingId is null)
		{
			return details;
		}

		using var activity = SammlarrTelemetry.StartMetadataRatings(ratingProvider);
		var ratings = await ratingIndex[ratingProvider].GetRatingsAsync(ratingId, cancellationToken);
		return MediaMerger.WithRatings(details, ratings);
	}

	private async Task<MediaMatch> EnrichAsync(MediaMatch details, MediaSearchProvider searchProvider, CancellationToken cancellationToken)
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

			using var activity = SammlarrTelemetry.StartMetadataEnrich(providerId, partial.MissingFields);
			var enrichment = await enricherIndex[provider].LookUpAsync(providerId, cancellationToken);
			if (enrichment is null)
			{
				continue;
			}
			details = enrichment.Value switch
			{
				IMedia complete => partial.MergeWith(complete),
				IPartialMedia other => partial.MergeWith(other),
				_ => details,
			};
		}
		return details;
	}
}
