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
	private static readonly MediaProvider[] EnricherChain = [MediaProvider.Plex, MediaProvider.Tmdb];

	public async Task<IMediaDetailResult> GetAsync(IMediaId anchor, CancellationToken cancellationToken = default)
	{
		var config = await configRepository.GetAsync(cancellationToken);
		var cached = await repository.GetByMediaIdAsync(anchor, cancellationToken);

		if (cached is not null && IsFresh(cached.FetchedAt))
		{
			return await BackfillRatingsIfMissingAsync(anchor, cached.Details, config.RatingProvider, cancellationToken);
		}

		return await FetchFreshAsync(anchor, config, cancellationToken);
	}

	public Task ReportStaleAsync(IMediaId anchor, CancellationToken cancellationToken = default) => repository.RemoveAsync(anchor, cancellationToken);

	private bool IsFresh(DateTimeOffset fetchedAt) => dateTimeProvider.Now - fetchedAt < StaleAfter;

	private async Task<IMediaDetailResult> BackfillRatingsIfMissingAsync(
		IMediaId anchor,
		IMediaDetailResult details,
		RatingProvider ratingProvider,
		CancellationToken cancellationToken
	)
	{
		var configuredSource = ratingProvider.AsRatingSource();
		if (details.Ratings.Any(rating => rating.Source == configuredSource))
		{
			return details;
		}

		var withRatings = await ApplyRatingsAsync(details, ratingProvider, cancellationToken);
		if (ReferenceEquals(withRatings, details))
		{
			return details;
		}

		await repository.UpsertAsync(anchor, withRatings, cancellationToken);
		return withRatings;
	}

	private async Task<IMediaDetailResult> FetchFreshAsync(IMediaId anchor, ProviderConfig config, CancellationToken cancellationToken)
	{
		var details = await searchIndex[config.SearchProvider].LookUpAsync(anchor, cancellationToken);
		details = await EnrichAsync(details, config.SearchProvider.AsMediaProvider(), cancellationToken);
		details = await ApplyRatingsAsync(details, config.RatingProvider, cancellationToken);
		await repository.UpsertAsync(anchor, details, cancellationToken);
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

	private async Task<IMediaDetailResult> EnrichAsync(IMediaDetailResult details, MediaProvider primaryProvider, CancellationToken cancellationToken)
	{
		foreach (var provider in EnricherChain)
		{
			if (provider == primaryProvider)
			{
				continue;
			}
			if (MetadataCompleteness.IsComplete(details))
			{
				break;
			}

			var idForProvider = details.AdditionalIds.FirstOrDefault(id => id.Source == provider);
			if (idForProvider is null)
			{
				continue;
			}

			var enrichment = await enricherIndex[provider].LookUpAsync(idForProvider, cancellationToken);
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
