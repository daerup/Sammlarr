using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Contracts;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Wantlist;
using Core.Wantlist.Sync;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal sealed class MediaService(
	MediaIdResolver idResolver,
	DetailsFetcher detailsFetcher,
	MediaIdentityCoordinator coordinator,
	MediaWriteBufferFactory bufferFactory,
	ActiveSink activeSink,
	IMediaRepository mediaRepository,
	IDateTimeProvider dateTimeProvider,
	ILogger<MediaService> logger
) : IMediaService
{
	internal const int RecommendedConcurrency = 100;
	private const int FirstBatchSize = 10;
	private const int MaxBatchSize = 200;
	private const int PrefetchCapacity = 1000;

	public async IAsyncEnumerable<Result<MediaIdentity>> IdentifyAsync(
		IEnumerable<IdentifyRequest> requests,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var materialized = requests.ToList();
		if (materialized.Count == 0)
		{
			yield break;
		}

		await using var buffer = bufferFactory.Create();
		var building = new Dictionary<IExternalMediaId, MediaIdentity>();
		var sink = await activeSink.ResolveAsync(cancellationToken);
		var identityMatches = await IdentifyBatchAsync(materialized, sink, building, buffer, cancellationToken);
		foreach (var (request, identityMatch) in materialized.Zip(identityMatches))
		{
			yield return ToIdentityResult(identityMatch);
		}
	}

	public async IAsyncEnumerable<Result<MatchedMedia>> GetDetailsAsync(
		IEnumerable<MediaId> ids,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var materialized = ids.ToList();
		if (materialized.Count == 0)
		{
			yield break;
		}

		await using var buffer = bufferFactory.Create();
		var stored = await LoadByInternalsAsync(materialized, cancellationToken);
		var pipeline = materialized.SelectAwait(id => LoadStoredDetailsAsync(id, stored, buffer, cancellationToken), RecommendedConcurrency, cancellationToken);
		await foreach (var result in pipeline)
		{
			yield return result;
		}
	}

	public async IAsyncEnumerable<TMedia> GetDetailsAsync<TMedia>(
		IEnumerable<IListItem<TMedia>> items,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var listed = items.ToList();
		var pipeline = GetDetailsAsync(listed.Select(item => item.MediaId), cancellationToken).ZipAwait(listed, cancellationToken);
		await foreach (var (media, item) in pipeline.WhereSuccessAwait().WithCancellation(cancellationToken))
		{
			yield return item.With(media);
		}
	}

	public async IAsyncEnumerable<MediaMatchResult> IdentifyWithDetailsAsync(
		IAsyncEnumerable<IdentifyRequest> requests,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		await using var buffer = bufferFactory.Create();
		var sink = await activeSink.ResolveAsync(cancellationToken);
		var building = new Dictionary<IExternalMediaId, MediaIdentity>();
		var size = FirstBatchSize;
		var batch = new List<IdentifyRequest>(size);
		await using var source = requests.PrefetchAsync(PrefetchCapacity, cancellationToken).GetAsyncEnumerator(cancellationToken);
		ExceptionDispatchInfo? failure = null;

		while (true)
		{
			bool moved;
			try
			{
				moved = await source.MoveNextAsync();
			}
			catch (Exception ex)
			{
				failure = ExceptionDispatchInfo.Capture(ex);
				break;
			}

			if (!moved)
			{
				break;
			}

			batch.Add(source.Current);
			if (batch.Count < size)
			{
				continue;
			}

			await foreach (var result in IdentifyAndLoadAsync(batch, sink, building, buffer, cancellationToken))
			{
				yield return result;
			}

			size = Math.Min(size * 2, MaxBatchSize);
			batch = new List<IdentifyRequest>(size);
		}

		if (batch.Count > 0 && failure?.SourceException is not OperationCanceledException)
		{
			await foreach (var result in IdentifyAndLoadAsync(batch, sink, building, buffer, cancellationToken))
			{
				yield return result;
			}
		}

		failure?.Throw();
	}

	private async IAsyncEnumerable<MediaMatchResult> IdentifyAndLoadAsync(
		IReadOnlyList<IdentifyRequest> batch,
		IMediaSink sink,
		Dictionary<IExternalMediaId, MediaIdentity> building,
		MediaWriteBuffer buffer,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		var identityMatches = await IdentifyBatchAsync(batch, sink, building, buffer, cancellationToken);
		var pipeline = batch
			.Zip(identityMatches)
			.SelectAwait(pair => LoadRequestDetailsAsync(pair.First, pair.Second, buffer, cancellationToken), RecommendedConcurrency, cancellationToken);
		await foreach (var result in pipeline)
		{
			yield return result;
		}
	}

	public async Task<IReadOnlyList<MediaIdentity>> RegisterAsync(
		IReadOnlyList<IReadOnlyList<IExternalMediaId>> candidateGroups,
		CancellationToken cancellationToken = default
	)
	{
		if (candidateGroups.Count == 0)
		{
			return [];
		}
		await using var buffer = bufferFactory.Create();
		var preliminary = candidateGroups.Select(g => new MediaIdentity(MediaId.New(), [.. g])).ToList();
		return await coordinator.CoordinateAsync(preliminary, buffer, cancellationToken);
	}

	public Task InvalidateAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		return mediaRepository.RemoveDetailsAsync([.. ids], cancellationToken);
	}

	public async Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetSinkIdsAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		var sink = await activeSink.ResolveAsync(cancellationToken);
		var wanted = ids.ToList();
		var sources = new HashSet<MediaProvider>();
		foreach (var type in Enum.GetValues<MediaType>())
		{
			if (sink.AddressedBy(type) is Some<MediaProvider>(var addressed))
			{
				sources.Add(addressed);
			}
		}
		var found = new Dictionary<MediaId, IExternalMediaId>();
		foreach (var source in sources)
		{
			foreach (var (id, external) in await mediaRepository.GetExternalsAsync(wanted, source, cancellationToken))
			{
				if (sink.AddressedBy(external.Type) is Some<MediaProvider>(var expected) && expected == source)
				{
					found[id] = external;
				}
			}
		}
		return found;
	}

	private async Task<IReadOnlyList<Result<IdentityMatch>>> IdentifyBatchAsync(
		IReadOnlyList<IdentifyRequest> requests,
		IMediaSink sink,
		Dictionary<IExternalMediaId, MediaIdentity> building,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var allCandidates = requests.SelectMany(r => r.Candidates).Distinct().ToList();
		var stored = await LoadByExternalsAsync(allCandidates, cancellationToken);
		var preliminary = await requests
			.SelectAwait(req => IdentifyOneAsync(req, sink, stored, buffer, cancellationToken), RecommendedConcurrency, cancellationToken)
			.ToListAsync(cancellationToken);
		return await coordinator.CoordinateIdentityMatchesAsync(preliminary, building, buffer, cancellationToken);
	}

	private async ValueTask<Result<IdentityMatch>> IdentifyOneAsync(
		IdentifyRequest request,
		IMediaSink sink,
		IReadOnlyDictionary<IExternalMediaId, StoredMedia> stored,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		if (sink.AddressedBy(request.Type) is not Some<MediaProvider>(var target))
		{
			return Error.From($"The sink cannot address {request.Type} titles.");
		}
		if (FindStored(request, target, stored, buffer) is Some<IdentityMatch>(var found))
		{
			return Success.Of(found);
		}
		return await FindSinkIdAsync(request, target, cancellationToken);
	}

	private static Optional<IdentityMatch> FindStored(
		IdentifyRequest request,
		MediaProvider target,
		IReadOnlyDictionary<IExternalMediaId, StoredMedia> stored,
		MediaWriteBuffer buffer
	)
	{
		var matches = request.Candidates.Select(stored.GetValueOrDefault).Where(r => r is not null).Select(r => r!).DistinctBy(r => r.Identity.Id).ToList();
		if (matches.Count > 1)
		{
			throw new MediaIdentityConflictException([.. matches.Select(m => m.Identity.Id)], [.. request.Candidates]);
		}
		var match = matches.FirstOrDefault();
		if (match is null || match.Identity.Externals.OfProvider(target) is null)
		{
			return new None();
		}
		var matched = match.Identity;
		var missing = request.Candidates.Where(c => !matched.Externals.Contains(c)).ToList();
		if (missing.Count == 0)
		{
			return Some.Of(new IdentityMatch(matched, Some.Of(match)));
		}
		var merged = matched with { Externals = matched.Externals + missing };
		buffer.AddIdentity(merged);
		return Some.Of(new IdentityMatch(merged, Some.Of(match)));
	}

	private async ValueTask<Result<IdentityMatch>> FindSinkIdAsync(IdentifyRequest request, MediaProvider target, CancellationToken cancellationToken)
	{
		return await idResolver
			.ResolveAsync(request, target, cancellationToken)
			.Map(externals => new IdentityMatch(new MediaIdentity(MediaId.New(), [.. externals]), new None()));
	}

	private async ValueTask<Result<MatchedMedia>> LoadStoredDetailsAsync(
		MediaId id,
		IReadOnlyDictionary<MediaId, StoredMedia> stored,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		if (!stored.TryGetValue(id, out var row))
		{
			return Error.From("identity not found");
		}
		return await LoadDetailsAsync(row.Identity, Some.Of(row), buffer, cancellationToken);
	}

	private async ValueTask<MediaMatchResult> LoadRequestDetailsAsync(
		IdentifyRequest request,
		Result<IdentityMatch> identityMatch,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		if (identityMatch is not Success<IdentityMatch>(var r))
		{
			return request.Unmatched();
		}
		var loaded = await LoadDetailsAsync(r.Identity, r.Stored, buffer, cancellationToken);
		return loaded is Success<MatchedMedia>(var matched) ? matched : request.Unmatched();
	}

	private async ValueTask<Result<MatchedMedia>> LoadDetailsAsync(
		MediaIdentity identity,
		Optional<StoredMedia> stored,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var cached = stored.Map(row => row.Cached);
		return await cached
			.When(details => details.IsFresh(dateTimeProvider.Now))
			.Match(
				fresh => ValueTask.FromResult<Result<MatchedMedia>>(Success.Of(new MatchedMedia(identity, fresh.Details))),
				() => FetchAsync(identity, stored, cached, buffer, cancellationToken)
			);
	}

	private async ValueTask<Result<MatchedMedia>> FetchAsync(
		MediaIdentity identity,
		Optional<StoredMedia> stored,
		Optional<CachedDetails> cached,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		try
		{
			var fetched = await detailsFetcher.FetchAsync(identity, buffer, cancellationToken);
			return fetched.Match(_ => fetched, error => Fallback(identity, stored, cached, error));
		}
		catch (Exception ex)
		{
			logger.Log(stored.Match(LogLevel.Warning, LogLevel.Error), ex, "Media details failed for {MediaId}", identity.Id);
			return Fallback(identity, stored, cached, Error.From(ex));
		}
	}

	private static Result<MatchedMedia> Fallback(MediaIdentity identity, Optional<StoredMedia> stored, Optional<CachedDetails> cached, Error error)
	{
		return cached
			.Map(stale => stale.Details)
			.Or(() => stored.Map(_ => MediaDetails.Empty(identity.Type)))
			.Map(details => new MatchedMedia(identity, details))
			.ToResult(error);
	}

	private static Result<MediaIdentity> ToIdentityResult(Result<IdentityMatch> identityMatch) => identityMatch.Map(r => r.Identity);

	private async Task<IReadOnlyDictionary<MediaId, StoredMedia>> LoadByInternalsAsync(IReadOnlyList<MediaId> ids, CancellationToken cancellationToken)
	{
		if (ids.Count == 0)
		{
			return new Dictionary<MediaId, StoredMedia>();
		}
		var rows = await mediaRepository.GetAsync(ids, cancellationToken);
		return rows.ToDictionary(r => r.Identity.Id);
	}

	private async Task<IReadOnlyDictionary<IExternalMediaId, StoredMedia>> LoadByExternalsAsync(
		IReadOnlyList<IExternalMediaId> ids,
		CancellationToken cancellationToken
	)
	{
		if (ids.Count == 0)
		{
			return new Dictionary<IExternalMediaId, StoredMedia>();
		}
		var rows = await mediaRepository.GetByExternalsAsync(ids, cancellationToken);
		return rows.SelectMany(r => r.Identity.Externals.Select(e => (External: e, Row: r))).ToDictionary(t => t.External, t => t.Row);
	}
}

file static class IdentifyRequestExtensions
{
	extension(IdentifyRequest request)
	{
		public MediaMatchResult Unmatched()
		{
			var hint = request.Hints is Some<IdentifyHints>(var hints) ? hints : null;
			return new UnmatchedMedia(
				[.. request.Candidates],
				MediaDetails.Empty(hint?.MediaType ?? request.Type) with
				{
					Title = hint?.Title,
					ReleaseDate = hint is { Year: > 0 } year ? new DateOnly(year.Year, 1, 1) : null,
				}
			);
		}
	}
}
