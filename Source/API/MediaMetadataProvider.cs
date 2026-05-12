using System.Diagnostics;
using System.Runtime.CompilerServices;
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
	private const int MaxFetchConcurrency = 10;

	public async IAsyncEnumerable<IMediaDetailResult> GetAsync(
		IReadOnlyList<IMediaId> mediaIds,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		if (mediaIds.Count == 0)
		{
			yield break;
		}

		using var activity = SammlarrTelemetry.StartMetadataBatch(mediaIds.Count);
		var batchContext = activity?.Context ?? default;
		var config = await configRepository.GetAsync(cancellationToken);

		using var gate = new SemaphoreSlim(MaxFetchConcurrency);
		var fetchTasks = new List<Task<Resolution>>();
		var byState = new Dictionary<ResolutionState, List<Resolution>>
		{
			[ResolutionState.ReturnCached] = [],
			[ResolutionState.FetchFresh] = [],
			[ResolutionState.Hydrate] = [],
		};

		foreach (var id in mediaIds)
		{
			var (state, resolution) = Classify(id, await repository.GetByMediaIdAsync(id, cancellationToken));
			byState[state].Add(resolution);

			if (state == ResolutionState.ReturnCached)
			{
				yield return resolution.Details!;
			}
			else
			{
				fetchTasks.Add(RunWithGateAsync(resolution, config, gate, batchContext, cancellationToken));
			}
		}

		SammlarrTelemetry.RecordBatchCacheBreakdown(
			activity,
			byState[ResolutionState.ReturnCached].Count,
			byState[ResolutionState.FetchFresh].Count,
			byState[ResolutionState.Hydrate].Count
		);

		if (fetchTasks.Count == 0)
		{
			yield break;
		}

		var completed = new List<Resolution>(fetchTasks.Count);
		await foreach (var task in Task.WhenEach(fetchTasks).WithCancellation(cancellationToken))
		{
			var resolution = await task;
			completed.Add(resolution);
			yield return resolution.Details!;
		}

		await UpsertCompletedAsync(completed, batchContext, cancellationToken);
	}

	private async Task UpsertCompletedAsync(List<Resolution> completed, ActivityContext batchContext, CancellationToken cancellationToken)
	{
		using var activity = SammlarrTelemetry.StartMetadataUpsert(completed.Count, batchContext);
		await repository.UpsertManyAsync(completed.Select(r => new MediaMetadataUpsert(r.Id, r.Details!)).ToList(), cancellationToken);
	}

	public async Task InvalidateAsync(IEnumerable<IMediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		foreach (var mediaId in mediaIds)
		{
			await repository.RemoveAsync(mediaId, cancellationToken);
		}
	}

	private (ResolutionState State, Resolution Resolution) Classify(IMediaId mediaId, CachedMediaMetadata? cached)
	{
		if (cached is null)
		{
			return (ResolutionState.FetchFresh, new Resolution { Id = mediaId });
		}

		var sinceFetched = dateTimeProvider.Now - cached.FetchedAt;
		if (sinceFetched >= StaleAfter)
		{
			return (ResolutionState.FetchFresh, new Resolution { Id = mediaId });
		}

		if (MetadataCompleteness.GetMissingFields(cached.Details).Count == 0 || sinceFetched < HydrationCooldown)
		{
			return (ResolutionState.ReturnCached, new Resolution { Id = mediaId, Details = cached.Details });
		}

		return (ResolutionState.Hydrate, new Resolution { Id = mediaId, Details = cached.Details });
	}

	private async Task<Resolution> RunWithGateAsync(
		Resolution resolution,
		ProviderConfig config,
		SemaphoreSlim gate,
		ActivityContext parentContext,
		CancellationToken cancellationToken
	)
	{
		var label = resolution.Details is null ? nameof(ResolutionState.FetchFresh) : nameof(ResolutionState.Hydrate);
		using var activity = SammlarrTelemetry.StartMetadataItem(resolution.Id, label, parentContext);

		await gate.WaitAsync(cancellationToken);
		try
		{
			resolution.Details = await ExecuteFetchAsync(resolution, config, cancellationToken);
			SammlarrTelemetry.TagItemSuccess(activity, resolution.Details is not null);
			return resolution;
		}
		finally
		{
			gate.Release();
		}
	}

	private async Task<IMediaDetailResult> ExecuteFetchAsync(Resolution resolution, ProviderConfig config, CancellationToken cancellationToken)
	{
		var baseDetail = resolution.Details ?? await LookUpAsync(resolution.Id, config.SearchProvider, cancellationToken);
		baseDetail = await EnrichAsync(baseDetail, config.SearchProvider, cancellationToken);
		baseDetail = await ApplyRatingsAsync(baseDetail, config.RatingProvider, cancellationToken);
		return baseDetail;
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

	private sealed record Resolution
	{
		public required IMediaId Id { get; init; }
		public IMediaDetailResult? Details { get; set; }
	}
}
