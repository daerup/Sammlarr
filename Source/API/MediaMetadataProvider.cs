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

		var resolutions = await mediaIds
			.ToAsyncEnumerable()
			.SelectAwait(async id => Classify(id, await repository.GetByMediaIdAsync(id, cancellationToken)))
			.ToListAsync(cancellationToken);

		var hits = 0;
		var misses = 0;
		var stale = 0;
		foreach (var resolution in resolutions)
		{
			switch (resolution.State)
			{
				case ResolutionState.ReturnCached:
					SammlarrTelemetry.RecordCacheHit(resolution.Id.Type);
					hits++;
					break;
				case ResolutionState.FetchFresh:
					SammlarrTelemetry.RecordCacheMiss(resolution.Id.Type);
					misses++;
					break;
				case ResolutionState.Hydrate:
					SammlarrTelemetry.RecordCacheStale(resolution.Id.Type);
					stale++;
					break;
			}
		}
		SammlarrTelemetry.SetBatchCacheBreakdown(activity, hits, misses, stale);

		foreach (var resolution in resolutions.Where(r => r.State == ResolutionState.ReturnCached))
		{
			yield return resolution.Details!;
		}

		var pending = resolutions.Where(r => r.State != ResolutionState.ReturnCached).ToList();
		var completed = new List<Resolution>(pending.Count);

		if (pending.Count > 0)
		{
			using var gate = new SemaphoreSlim(MaxFetchConcurrency);
			var tasks = pending.Select(r => RunWithGateAsync(r, config, gate, batchContext, cancellationToken)).ToList();

			await foreach (var task in Task.WhenEach(tasks).WithCancellation(cancellationToken))
			{
				var resolution = await task;
				completed.Add(resolution);
				yield return resolution.Details!;
			}
		}

		if (completed.Count > 0)
		{
			await UpsertCompletedAsync(completed, batchContext, cancellationToken);
		}
	}

	// Async iterators drift Activity.Current across many yield/MoveNextAsync round-trips: the iterator-scoped using-var span
	// stays alive (Dispose hasn't run), but post-yield code resumes with the consumer's ambient activity instead of ours.
	// Explicit parent context anchors the upsert under the batch regardless of Activity.Current.
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

	private Resolution Classify(IMediaId mediaId, CachedMediaMetadata? cached)
	{
		if (cached is null)
		{
			return new Resolution { Id = mediaId, State = ResolutionState.FetchFresh };
		}

		var sinceFetched = dateTimeProvider.Now - cached.FetchedAt;
		if (sinceFetched >= StaleAfter)
		{
			return new Resolution { Id = mediaId, State = ResolutionState.FetchFresh };
		}

		if (MetadataCompleteness.GetMissingFields(cached.Details).Count == 0 || sinceFetched < HydrationCooldown)
		{
			return new Resolution
			{
				Id = mediaId,
				State = ResolutionState.ReturnCached,
				Details = cached.Details,
			};
		}

		return new Resolution
		{
			Id = mediaId,
			State = ResolutionState.Hydrate,
			Details = cached.Details,
		};
	}

	private async Task<Resolution> RunWithGateAsync(
		Resolution resolution,
		ProviderConfig config,
		SemaphoreSlim gate,
		ActivityContext parentContext,
		CancellationToken cancellationToken
	)
	{
		using var activity = SammlarrTelemetry.StartMetadataItem(resolution.Id, resolution.State.ToString(), parentContext);

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
		var baseDetail = resolution.State switch
		{
			ResolutionState.FetchFresh => await LookUpAsync(resolution.Id, config.SearchProvider, cancellationToken),
			ResolutionState.Hydrate => resolution.Details!,
		};

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
		public required ResolutionState State { get; init; }
		public IMediaDetailResult? Details { get; set; }
	}
}
