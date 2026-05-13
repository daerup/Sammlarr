using System.Diagnostics;
using API.Metadata;
using Contracts;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Settings;
using Shared;
using Shared.Telemetry;

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

	public async Task<IMediaDetailResult> GetAsync(IMediaId mediaId, CancellationToken cancellationToken = default, ActivityContext parent = default)
	{
		using var activity = SammlarrTelemetry.StartMetadataItem(mediaId, parent);

		var cached = await repository.GetByMediaIdAsync(mediaId, cancellationToken);
		var (state, cachedDetails) = Classify(cached);
		SammlarrTelemetry.TagItemState(activity, state.ToString());

		switch (state)
		{
			case ResolutionState.ReturnCached:
				SammlarrTelemetry.RecordCacheHit(mediaId.Type);
				return cachedDetails!;
			case ResolutionState.FetchFresh:
				SammlarrTelemetry.RecordCacheMiss(mediaId.Type);
				break;
			case ResolutionState.Hydrate:
				SammlarrTelemetry.RecordCacheStale(mediaId.Type);
				break;
		}

		var config = await configRepository.GetAsync(cancellationToken);

		var detail = cachedDetails ?? await LookUpAsync(mediaId, config.SearchProvider, cancellationToken);
		detail = await EnrichAsync(detail, config.SearchProvider, cancellationToken);
		detail = await ApplyRatingsAsync(detail, config.RatingProvider, cancellationToken);

		await repository.UpsertManyAsync([new MediaMetadataUpsert(mediaId, detail)], cancellationToken);

		SammlarrTelemetry.TagItemSuccess(activity, true);
		return detail;
	}

	public Task InvalidateAsync(IEnumerable<IMediaId> mediaIds, CancellationToken cancellationToken = default) =>
		repository.RemoveManyAsync([.. mediaIds], cancellationToken);

	public Task PurgeAllAsync(CancellationToken cancellationToken = default) => repository.PurgeAllAsync(cancellationToken);

	private (ResolutionState State, IMediaDetailResult? Cached) Classify(CachedMediaMetadata? cached)
	{
		if (cached is null)
		{
			return (ResolutionState.FetchFresh, null);
		}

		var sinceFetched = dateTimeProvider.Now - cached.FetchedAt;
		if (sinceFetched >= StaleAfter)
		{
			return (ResolutionState.FetchFresh, null);
		}

		if (MetadataCompleteness.GetMissingFields(cached.Details).Count == 0 || sinceFetched < HydrationCooldown)
		{
			return (ResolutionState.ReturnCached, cached.Details);
		}

		return (ResolutionState.Hydrate, cached.Details);
	}

	private async Task<IMediaDetailResult> LookUpAsync(IMediaId id, MediaSearchProvider provider, CancellationToken cancellationToken)
	{
		using var activity = SammlarrTelemetry.StartMetadataLookup(provider, id);
		return await searchIndex[provider].LookUpAsync(id, cancellationToken);
	}

	private async Task<IMediaDetailResult> ApplyRatingsAsync(IMediaDetailResult details, RatingProvider ratingProvider, CancellationToken cancellationToken)
	{
		var ratingId = FindId(details, ratingProvider.AsMediaProvider());
		if (ratingId is null)
		{
			return details;
		}

		using var activity = SammlarrTelemetry.StartMetadataRatings(ratingProvider);
		var ratings = await ratingIndex[ratingProvider].GetRatingsAsync(ratingId, cancellationToken);
		return ratings.Count == 0 ? details : MetadataMerger.WithRatings(details, ratings);
	}

	private async Task<IMediaDetailResult> EnrichAsync(IMediaDetailResult details, MediaSearchProvider searchProvider, CancellationToken cancellationToken)
	{
		var primaryProvider = searchProvider.AsMediaProvider();
		foreach (var provider in EnricherChain.Where(p => p != primaryProvider))
		{
			var missing = MetadataCompleteness.GetMissingFields(details);
			if (missing.Count == 0)
			{
				break;
			}

			var providerId = FindId(details, provider);
			if (providerId is null)
			{
				continue;
			}

			using var activity = SammlarrTelemetry.StartMetadataEnrich(providerId, missing);
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

	private enum ResolutionState
	{
		ReturnCached,
		FetchFresh,
		Hydrate,
	}
}
