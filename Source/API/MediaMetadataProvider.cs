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
	IMediaRepository mediaRepository,
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

	public async Task<IMediaDetailResult?> GetAsync(MediaId mediaId, CancellationToken cancellationToken = default)
	{
		using var activity = SammlarrTelemetry.StartMetadataItem(mediaId);

		var cached = await repository.GetByMediaIdAsync(mediaId, cancellationToken);
		var (outcome, cachedDetails) = Classify(cached);
		SammlarrTelemetry.TagItemOutcome(activity, outcome, cachedDetails?.MediaType ?? MediaType.Movie);

		if (outcome is MetadataItemOutcome.Hit)
		{
			return cachedDetails!;
		}

		if (await mediaRepository.FindAsync(mediaId, cancellationToken) is not { } identity)
		{
			SammlarrTelemetry.TagItemSuccess(activity, false);
			return null;
		}

		var config = await configRepository.GetAsync(cancellationToken);
		var detail = cachedDetails ?? await LookUpAsync(mediaId, identity.Externals, config.SearchProvider, cancellationToken);
		detail = await EnrichAsync(detail, config.SearchProvider, cancellationToken);
		detail = await ApplyRatingsAsync(detail, config.RatingProvider, cancellationToken);

		var known = identity.Externals.ToHashSet();
		var discovered = detail.Externals.Where(e => !known.Contains(e)).ToList();
		if (discovered.Count > 0)
		{
			await mediaRepository.UpsertAsync([.. identity.Externals, .. discovered], cancellationToken);
		}

		await repository.UpsertManyAsync([new MediaMetadataUpsert(mediaId, detail)], cancellationToken);

		SammlarrTelemetry.TagItemSuccess(activity, true);
		return detail;
	}

	public Task InvalidateAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default) =>
		repository.RemoveManyAsync([.. mediaIds], cancellationToken);

	private (MetadataItemOutcome Outcome, IMediaDetailResult? Cached) Classify(CachedMediaMetadata? cached)
	{
		if (cached is null)
		{
			return (MetadataItemOutcome.Miss, null);
		}

		var sinceFetched = dateTimeProvider.Now - cached.FetchedAt;
		if (sinceFetched >= StaleAfter)
		{
			return (MetadataItemOutcome.Stale, null);
		}

		if (MetadataCompleteness.GetMissingFields(cached.Details).Count == 0 || sinceFetched < HydrationCooldown)
		{
			return (MetadataItemOutcome.Hit, cached.Details);
		}

		return (MetadataItemOutcome.Partial, cached.Details);
	}

	private async Task<IMediaDetailResult> LookUpAsync(
		MediaId mediaId,
		IReadOnlyList<IExternalMediaId> externals,
		MediaSearchProvider provider,
		CancellationToken cancellationToken
	)
	{
		var providerMedia = provider.AsMediaProvider();
		var external = externals.FirstOrDefault(e => e.Source == providerMedia) ?? externals.First();
		using var activity = SammlarrTelemetry.StartMetadataLookup(provider, external);
		var detail = await searchIndex[provider].LookUpAsync(external, cancellationToken);
		return StampMediaId(detail, mediaId);
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

	private static IExternalMediaId? FindId(IMediaDetailResult details, MediaProvider provider) =>
		details.Externals.FirstOrDefault(id => id.Source == provider);

	private static IMediaDetailResult StampMediaId(IMediaDetailResult detail, MediaId mediaId) =>
		detail switch
		{
			MovieDetailResult m => m with { Id = mediaId },
			ShowDetailResult s => s with { Id = mediaId },
			UnresolvedMediaResult u => u with { Id = mediaId },
			_ => detail,
		};
}
