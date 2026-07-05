using System.Diagnostics;
using System.Runtime.CompilerServices;
using Contracts;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Domain.Media;
using Domain.Media.Metadata;
using Microsoft.Extensions.Logging;
using Shared;

namespace Core.Media;

internal sealed class MediaService(
	MediaIdResolver idResolver,
	MediaMetadataResolver metadataResolver,
	MediaIdentityCoordinator coordinator,
	MediaResolutionBufferFactory bufferFactory,
	IMediaRepository mediaRepository,
	IDateTimeProvider dateTimeProvider,
	ILogger<MediaService> logger
) : IMediaService
{
	public async IAsyncEnumerable<Result<MediaIdentity>> IdentifyAsync(
		IEnumerable<ResolveRequest> requests,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var materialized = requests.ToList();
		if (materialized.Count == 0)
		{
			yield break;
		}

		await using var buffer = bufferFactory.Create();
		var resolutions = await ResolveBatchAsync(materialized, buffer, cancellationToken);
		foreach (var (request, resolution) in materialized.Zip(resolutions))
		{
			yield return ToIdentityResult(resolution);
		}
	}

	public async IAsyncEnumerable<Result<IdentifiedMedia>> GetDetailsAsync(
		IEnumerable<MediaId> mediaIds,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var materialized = mediaIds.ToList();
		if (materialized.Count == 0)
		{
			yield break;
		}

		await using var buffer = bufferFactory.Create();
		var cached = await LoadByInternalsAsync(materialized, cancellationToken);
		var pipeline = materialized.SelectAwait(
			id => RealizeFromCacheAsync(id, cached, buffer, cancellationToken),
			IMediaService.RecommendedConcurrency,
			cancellationToken
		);
		await foreach (var result in pipeline)
		{
			yield return result;
		}
	}

	public async IAsyncEnumerable<MediaLookup> ResolveDetailsAsync(
		IEnumerable<ResolveRequest> requests,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var materialized = requests.ToList();
		if (materialized.Count == 0)
		{
			yield break;
		}

		await using var buffer = bufferFactory.Create();
		var resolutions = await ResolveBatchAsync(materialized, buffer, cancellationToken);
		var pipeline = materialized
			.Zip(resolutions)
			.SelectAwait(
				pair => RealizeFromRequestAsync(pair.First, pair.Second, buffer, cancellationToken),
				IMediaService.RecommendedConcurrency,
				cancellationToken
			);
		await foreach (var result in pipeline)
		{
			yield return result;
		}
	}

	public async Task<IReadOnlyList<MediaIdentity>> ResolveIdentitiesAsync(
		IReadOnlyList<IReadOnlyList<IExternalMediaId>> candidateGroups,
		CancellationToken cancellationToken = default
	)
	{
		if (candidateGroups.Count == 0)
		{
			return [];
		}
		await using var buffer = bufferFactory.Create();
		var preliminary = candidateGroups.Select(g => new MediaIdentity(MediaId.From(Guid.NewGuid()), [.. g])).ToList();
		return await coordinator.CoordinateAsync(preliminary, buffer, cancellationToken);
	}

	public Task InvalidateAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		return mediaRepository.RemoveMetadataAsync([.. mediaIds], cancellationToken);
	}

	private async Task<IReadOnlyList<Optional<ResolvedIdentity>>> ResolveBatchAsync(
		IReadOnlyList<ResolveRequest> requests,
		MediaResolutionBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var allCandidates = requests.SelectMany(r => r.Candidates).Distinct().ToList();
		var cached = await LoadByExternalsAsync(allCandidates, cancellationToken);
		var preliminary = await requests
			.SelectAwait(req => ResolveIdentityAsync(req, cached, buffer, cancellationToken), IMediaService.RecommendedConcurrency, cancellationToken)
			.ToListAsync(cancellationToken);
		return await coordinator.CoordinateResolutionsAsync(preliminary, buffer, cancellationToken);
	}

	private async ValueTask<Optional<ResolvedIdentity>> ResolveIdentityAsync(
		ResolveRequest request,
		IReadOnlyDictionary<IExternalMediaId, CachedMedia> cached,
		MediaResolutionBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		if (TryResolveFromCache(request, cached, buffer) is Some<ResolvedIdentity> hit)
		{
			return hit;
		}
		return await ResolveFromProviderAsync(request, cancellationToken);
	}

	private static Optional<ResolvedIdentity> TryResolveFromCache(
		ResolveRequest request,
		IReadOnlyDictionary<IExternalMediaId, CachedMedia> cached,
		MediaResolutionBuffer buffer
	)
	{
		var matches = request.Candidates.Select(cached.GetValueOrDefault).Where(r => r is not null).Select(r => r!).DistinctBy(r => r.Stored.Identity.Id).ToList();
		if (matches.Count > 1)
		{
			throw new MediaIdentityConflictException([.. matches.Select(m => m.Stored.Identity.Id)], [.. request.Candidates]);
		}
		var match = matches.FirstOrDefault();
		if (match is null || match.Stored.Externals.OfProvider(request.Target) is null)
		{
			return new None();
		}
		var matched = match.Stored.Identity;
		var missing = request.Candidates.Where(c => !matched.Externals.Contains(c)).ToList();
		if (missing.Count == 0)
		{
			return Some.Of(new ResolvedIdentity(matched, match));
		}
		var merged = matched with { Externals = matched.Externals + missing };
		buffer.AddIdentity(merged);
		return Some.Of(new ResolvedIdentity(merged, match));
	}

	private async ValueTask<Optional<ResolvedIdentity>> ResolveFromProviderAsync(ResolveRequest request, CancellationToken cancellationToken)
	{
		return await idResolver
			.ResolveAsync(request, cancellationToken)
			.Map(externals => new ResolvedIdentity(new MediaIdentity(MediaId.From(Guid.NewGuid()), [.. externals]), null));
	}

	private async ValueTask<Result<IdentifiedMedia>> RealizeFromCacheAsync(
		MediaId id,
		IReadOnlyDictionary<MediaId, CachedMedia> cached,
		MediaResolutionBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		if (!cached.TryGetValue(id, out var row))
		{
			return Error.From("identity not found");
		}
		return await RealizeAsync(row.Stored.Identity, row, buffer, cancellationToken);
	}

	private async ValueTask<MediaLookup> RealizeFromRequestAsync(
		ResolveRequest request,
		Optional<ResolvedIdentity> resolution,
		MediaResolutionBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		if (resolution is not Some<ResolvedIdentity>(var r))
		{
			return request.ToUnmatched();
		}
		var realized = await RealizeAsync(r.Identity, r.Cached, buffer, cancellationToken);
		return realized is Success<IdentifiedMedia>(var identified) ? identified : request.ToUnmatched();
	}

	private async ValueTask<Result<IdentifiedMedia>> RealizeAsync(
		MediaIdentity identity,
		CachedMedia? cached,
		MediaResolutionBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		try
		{
			if (cached is { } row && MediaFreshness.IsFresh(row.Stored, row.FetchedAt, dateTimeProvider.Now))
			{
				return Success.Of(row.Stored);
			}
			var resolved = await metadataResolver.ResolveAsync(identity, buffer, cancellationToken);
			if (resolved is Success<MediaLookup>(var lookup))
			{
				var identified = lookup.AsIdentified();
				if (identified is not null)
				{
					return Success.Of(identified.Value);
				}
			}
			return cached is { } fallback ? Success.Of(fallback.Stored) : Error.From("media could not be resolved");
		}
		catch (Exception ex)
		{
			if (cached is { } stale)
			{
				logger.LogWarning(ex, "Media realize failed for {MediaId}; serving stale cache", identity.Id);
				return Success.Of(stale.Stored);
			}
			logger.LogError(ex, "Media realize failed for {MediaId}; no cache fallback", identity.Id);
			return Error.From(ex);
		}
	}

	private static Result<MediaIdentity> ToIdentityResult(Optional<ResolvedIdentity> resolution) => resolution.Map(r => r.Identity).ToResult("unresolved");

	private async Task<IReadOnlyDictionary<MediaId, CachedMedia>> LoadByInternalsAsync(IReadOnlyList<MediaId> ids, CancellationToken cancellationToken)
	{
		if (ids.Count == 0)
		{
			return new Dictionary<MediaId, CachedMedia>();
		}
		var rows = await mediaRepository.GetAsync(ids, cancellationToken);
		return rows.ToDictionary(r => r.Stored.Identity.Id);
	}

	private async Task<IReadOnlyDictionary<IExternalMediaId, CachedMedia>> LoadByExternalsAsync(
		IReadOnlyList<IExternalMediaId> ids,
		CancellationToken cancellationToken
	)
	{
		if (ids.Count == 0)
		{
			return new Dictionary<IExternalMediaId, CachedMedia>();
		}
		var rows = await mediaRepository.GetByExternalsAsync(ids, cancellationToken);
		return rows.SelectMany(r => r.Stored.Externals.Select(e => (External: e, Row: r))).ToDictionary(t => t.External, t => t.Row);
	}
}

file static class ResolveRequestExtensions
{
	extension(ResolveRequest request)
	{
		public MediaLookup ToUnmatched()
		{
			var hint = request.Hints is Some<ResolutionHints>(var hints) ? hints : null;
			return new UnmatchedMedia
			{
				Externals = [.. request.Candidates],
				Title = hint?.Title ?? "",
				ReleaseDate = hint is { Year: > 0 } year ? new DateOnly(year.Year, 1, 1) : null,
			};
		}
	}
}
