using System.Diagnostics;
using System.Runtime.CompilerServices;
using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Core.Media.Metadata;
using Shared;
using Shared.Telemetry;

namespace Core.Media;

internal sealed class MediaService(
	MediaIdResolver idResolver,
	MediaMetadataResolver metadataResolver,
	MediaIdentityCoordinator coordinator,
	IMediaRepository mediaRepository,
	IMediaMetadataRepository metadataRepository,
	IMediaQuerySessionFactory sessionFactory,
	IDateTimeProvider dateTimeProvider
) : IMediaService
{
	public async IAsyncEnumerable<Result<Identification>> IdentifyAsync(
		IEnumerable<ResolveRequest> requests,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var materialized = requests.ToList();
		if (materialized.Count == 0)
		{
			yield break;
		}

		await using var buffer = new MediaResolutionBuffer(mediaRepository, metadataRepository);
		var resolutions = await ResolveBatchAsync(materialized, buffer, cancellationToken);
		foreach (var (request, resolution) in materialized.Zip(resolutions))
		{
			yield return ToIdentificationResult(resolution, request.Target);
		}
	}

	public async IAsyncEnumerable<Result<IMediaDetail>> GetDetailsAsync(
		IEnumerable<MediaId> mediaIds,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var materialized = mediaIds.ToList();
		if (materialized.Count == 0)
		{
			yield break;
		}

		await using var buffer = new MediaResolutionBuffer(mediaRepository, metadataRepository);
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

	public async IAsyncEnumerable<Result<IMediaDetail>> GetDetailsAsync(
		IEnumerable<ResolveRequest> requests,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var materialized = requests.ToList();
		if (materialized.Count == 0)
		{
			yield break;
		}

		await using var buffer = new MediaResolutionBuffer(mediaRepository, metadataRepository);
		var resolutions = await ResolveBatchAsync(materialized, buffer, cancellationToken);
		var pipeline = resolutions.SelectAwait(
			resolution => RealizeFromResolutionAsync(resolution, buffer, cancellationToken),
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
		await using var buffer = new MediaResolutionBuffer(mediaRepository, metadataRepository);
		var preliminary = candidateGroups.Select(g => new MediaIdentity(MediaId.From(Guid.NewGuid()), g)).ToList();
		return await coordinator.CoordinateAsync(preliminary, buffer, cancellationToken);
	}

	public Task InvalidateAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		return metadataRepository.RemoveManyAsync([.. mediaIds], cancellationToken);
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
		var title = request.Hints is Some<ResolutionHints>(var hints) ? hints.Title : null;
		using var activity = SammlarrTelemetry.StartIdResolution(request.Target, request.Candidates, title);
		if (TryResolveFromCache(request, cached, buffer) is Some<ResolvedIdentity> hit)
		{
			SammlarrTelemetry.TagIdResolutionOutcome(activity, IdResolutionOutcome.Hit, request.Target);
			return hit;
		}
		return await ResolveFromProviderAsync(request, activity, cancellationToken);
	}

	private static Optional<ResolvedIdentity> TryResolveFromCache(
		ResolveRequest request,
		IReadOnlyDictionary<IExternalMediaId, CachedMedia> cached,
		MediaResolutionBuffer buffer
	)
	{
		var matches = request.Candidates.Select(cached.GetValueOrDefault).Where(r => r is not null).Select(r => r!).DistinctBy(r => r.Detail.Identity.Id).ToList();
		if (matches.Count > 1)
		{
			throw new MediaIdentityConflictException([.. matches.Select(m => m.Detail.Identity.Id)], [.. request.Candidates]);
		}
		var match = matches.FirstOrDefault();
		if (match is null || match.Detail.Identity.Externals.All(e => e.Source != request.Target))
		{
			return new None();
		}
		var matched = match.Detail.Identity;
		var missing = request.Candidates.Where(c => !matched.Externals.Contains(c)).ToList();
		if (missing.Count == 0)
		{
			return Some.Of(new ResolvedIdentity(matched, match));
		}
		var merged = matched with { Externals = [.. matched.Externals, .. missing] };
		buffer.AddIdentity(merged);
		return Some.Of(new ResolvedIdentity(merged, match));
	}

	private async ValueTask<Optional<ResolvedIdentity>> ResolveFromProviderAsync(ResolveRequest request, Activity? activity, CancellationToken cancellationToken)
	{
		var discovered = await idResolver.ResolveAsync(request, cancellationToken);
		if (discovered is not Some<IReadOnlyList<IExternalMediaId>>(var externals))
		{
			SammlarrTelemetry.TagIdResolutionOutcome(activity, IdResolutionOutcome.Unresolved, request.Target);
			return new None();
		}
		SammlarrTelemetry.TagIdResolutionOutcome(activity, IdResolutionOutcome.Resolved, request.Target);
		return Some.Of(new ResolvedIdentity(new MediaIdentity(MediaId.From(Guid.NewGuid()), externals), null));
	}

	private async ValueTask<Result<IMediaDetail>> RealizeFromCacheAsync(
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
		return await RealizeAsync(row.Detail.Identity, row, buffer, cancellationToken);
	}

	private async ValueTask<Result<IMediaDetail>> RealizeFromResolutionAsync(
		Optional<ResolvedIdentity> resolution,
		MediaResolutionBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		if (resolution is not Some<ResolvedIdentity>(var r))
		{
			return Error.From("unresolved");
		}
		return await RealizeAsync(r.Identity, r.Cached, buffer, cancellationToken);
	}

	private async ValueTask<Result<IMediaDetail>> RealizeAsync(
		MediaIdentity identity,
		CachedMedia? cached,
		MediaResolutionBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		try
		{
			if (cached is { } row && MetadataFreshness.IsFresh(row.Detail, row.FetchedAt, dateTimeProvider))
			{
				return row.Detail;
			}
			return await metadataResolver.ResolveAsync(identity, buffer, cancellationToken);
		}
		catch (Exception ex)
		{
			return cached?.Detail is { } stale ? stale : Error.From(ex);
		}
	}

	private static Result<Identification> ToIdentificationResult(Optional<ResolvedIdentity> resolution, MediaProvider target)
	{
		if (resolution is not Some<ResolvedIdentity>(var r))
		{
			return Error.From("unresolved");
		}
		var targetExternal = r.Identity.Externals.First(e => e.Source == target);
		return new Identification(r.Identity.Id, r.Identity.Externals, targetExternal);
	}

	private async Task<IReadOnlyDictionary<MediaId, CachedMedia>> LoadByInternalsAsync(IReadOnlyList<MediaId> ids, CancellationToken cancellationToken)
	{
		if (ids.Count == 0)
		{
			return new Dictionary<MediaId, CachedMedia>();
		}
		await using var session = await sessionFactory.CreateAsync(cancellationToken);
		var idsQuery = mediaRepository.QueryMediaIds(session, ids);
		var rows = await metadataRepository.QueryMediaMetadataAsync(session, idsQuery, cancellationToken);
		return rows.ToDictionary(r => r.Detail.Identity.Id);
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
		await using var session = await sessionFactory.CreateAsync(cancellationToken);
		var idsQuery = mediaRepository.QueryMediaIds(session, ids);
		var rows = await metadataRepository.QueryMediaMetadataAsync(session, idsQuery, cancellationToken);
		return rows.SelectMany(r => r.Detail.Identity.Externals.Select(e => (External: e, Row: r))).ToDictionary(t => t.External, t => t.Row);
	}
}
