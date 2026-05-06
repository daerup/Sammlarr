using API.Metadata;
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
	IKeyedServiceIndex<RatingProvider, IMediaRatingService> ratingIndex,
	IProviderConfigRepository configRepository,
	IDateTimeProvider dateTimeProvider
) : IMediaMetadataProvider
{
	private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(7);
	private static readonly TimeSpan HydrationCooldown = TimeSpan.FromHours(1);
	private static readonly MediaProvider[] EnricherChain = [MediaProvider.Plex, MediaProvider.Tmdb];

	public async Task<IMediaDetailResult> GetAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var cached = await repository.GetByMediaIdAsync(mediaId, cancellationToken);
		if (cached is null)
		{
			return await FetchFreshAsync(mediaId, cancellationToken);
		}

		var sinceFetched = dateTimeProvider.Now - cached.FetchedAt;
		if (sinceFetched >= StaleAfter)
		{
			return await FetchFreshAsync(mediaId, cancellationToken);
		}

		if (MetadataCompleteness.IsComplete(cached.Details) || sinceFetched < HydrationCooldown)
		{
			return cached.Details;
		}

		return await HydrateAsync(mediaId, cached.Details, cancellationToken);
	}

	public async Task InvalidateAsync(IEnumerable<IMediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		foreach (var mediaId in mediaIds)
		{
			await repository.RemoveAsync(mediaId, cancellationToken);
		}
	}

	private async Task<IMediaDetailResult> FetchFreshAsync(IMediaId mediaId, CancellationToken cancellationToken)
	{
		var config = await configRepository.GetAsync(cancellationToken);
		var details = await searchIndex[config.SearchProvider].LookUpAsync(mediaId, cancellationToken);
		details = await EnrichAsync(details, config.SearchProvider, cancellationToken);
		details = await ApplyRatingsAsync(details, config.RatingProvider, cancellationToken);
		await repository.UpsertAsync(mediaId, details, cancellationToken);
		return details;
	}

	private async Task<IMediaDetailResult> HydrateAsync(IMediaId mediaId, IMediaDetailResult details, CancellationToken cancellationToken)
	{
		var config = await configRepository.GetAsync(cancellationToken);
		details = await EnrichAsync(details, config.SearchProvider, cancellationToken);
		details = await ApplyRatingsAsync(details, config.RatingProvider, cancellationToken);
		await repository.UpsertAsync(mediaId, details, cancellationToken);
		return details;
	}

	private async Task<IMediaDetailResult> ApplyRatingsAsync(IMediaDetailResult details, RatingProvider ratingProvider, CancellationToken cancellationToken)
	{
		var ratingId = FindId(details, ratingProvider.AsMediaProvider());
		if (ratingId is null)
		{
			return details;
		}

		var ratings = await ratingIndex[ratingProvider].GetRatingsAsync(ratingId, cancellationToken);
		return ratings.Count == 0 ? details : MetadataMerger.WithRatings(details, ratings);
	}

	private async Task<IMediaDetailResult> EnrichAsync(IMediaDetailResult details, MediaSearchProvider searchProvider, CancellationToken cancellationToken)
	{
		var primaryProvider = searchProvider.AsMediaProvider();
		foreach (var provider in EnricherChain.Where(p => p != primaryProvider))
		{
			if (MetadataCompleteness.IsComplete(details))
			{
				break;
			}

			var providerId = FindId(details, provider);
			if (providerId is null)
			{
				continue;
			}

			var enrichment = await enricherIndex[provider].LookUpAsync(providerId, cancellationToken);
			if (enrichment is not null)
			{
				details = MetadataMerger.FillMissing(details, enrichment);
			}
		}

		return details;
	}

	private static IMediaId? FindId(IMediaDetailResult details, MediaProvider provider) =>
		details.SourceId.Source == provider ? details.SourceId : details.AdditionalIds.FirstOrDefault(id => id.Source == provider);
}
