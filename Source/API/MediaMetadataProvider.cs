using System.Runtime.CompilerServices;
using API.Metadata;
using Contracts;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Settings;
using Shared;

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

		var config = await configRepository.GetAsync(cancellationToken);

		var resolutions = await mediaIds
			.ToAsyncEnumerable()
			.SelectAwait(async id => Classify(id, await repository.GetByMediaIdAsync(id, cancellationToken)))
			.ToListAsync(cancellationToken);

		foreach (var resolution in resolutions.Where(r => r.State == ResolutionState.ReturnCached))
		{
			yield return resolution.Details!;
		}

		var pending = resolutions.Where(r => r.State != ResolutionState.ReturnCached).ToList();
		var completed = new List<Resolution>(pending.Count);

		if (pending.Count > 0)
		{
			using var gate = new SemaphoreSlim(MaxFetchConcurrency);
			var tasks = pending.Select(r => RunWithGateAsync(r, config, gate, cancellationToken)).ToList();

			await foreach (var task in Task.WhenEach(tasks).WithCancellation(cancellationToken))
			{
				var resolution = await task;
				completed.Add(resolution);
				yield return resolution.Details!;
			}
		}

		if (completed.Count > 0)
		{
			await repository.UpsertManyAsync(completed.Select(r => new MediaMetadataUpsert(r.Id, r.Details!)).ToList(), cancellationToken);
		}
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

		if (MetadataCompleteness.IsComplete(cached.Details) || sinceFetched < HydrationCooldown)
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

	private async Task<Resolution> RunWithGateAsync(Resolution resolution, ProviderConfig config, SemaphoreSlim gate, CancellationToken cancellationToken)
	{
		await gate.WaitAsync(cancellationToken);
		try
		{
			resolution.Details = await ExecuteFetchAsync(resolution, config, cancellationToken);
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
			ResolutionState.FetchFresh => await searchIndex[config.SearchProvider].LookUpAsync(resolution.Id, cancellationToken),
			ResolutionState.Hydrate => resolution.Details!,
		};

		baseDetail = await EnrichAsync(baseDetail, config.SearchProvider, cancellationToken);
		baseDetail = await ApplyRatingsAsync(baseDetail, config.RatingProvider, cancellationToken);
		return baseDetail;
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
