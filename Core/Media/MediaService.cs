using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Wantlist;
using Core.Wantlist.Sync;
using Domain.Feed.Trace;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging;
using Observability.Feed;

namespace Core.Media;

internal sealed class MediaService(
	MediaIdResolver idResolver,
	DetailsFetcher detailsFetcher,
	MediaIdentityCoordinator coordinator,
	MediaWriteBufferFactory bufferFactory,
	ActiveSink activeSink,
	IMediaRepository mediaRepository,
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
		var handoffs = await IdentifyBatchAsync([.. materialized.Select(TracedRequest.Untraced)], sink.AddressedBy, building, buffer, cancellationToken);
		foreach (var (_, handoff) in materialized.Zip(handoffs))
		{
			yield return ToIdentityResult(handoff);
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
		var connected = await detailsFetcher.ConnectedAsync(cancellationToken);
		var pipeline = materialized.SelectAwait(
			(id, token) => LoadStoredDetailsAsync(id, stored, connected, buffer, token),
			RecommendedConcurrency,
			cancellationToken
		);
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
		var connected = await detailsFetcher.ConnectedAsync(cancellationToken);
		var building = new Dictionary<IExternalMediaId, MediaIdentity>();
		var size = FirstBatchSize;
		var batch = new List<IdentifyRequest>(size);
		await using var source = requests.PrefetchAsync(PrefetchCapacity, cancellationToken).GetAsyncEnumerator(cancellationToken);
		ExceptionDispatchInfo? failure = null;
		var listing = FeedRunSpans.Source();
		var pulled = 0;

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
			pulled++;
			if (batch.Count < size)
			{
				continue;
			}

			await foreach (var result in IdentifyAndLoadAsync(batch, sink, building, connected, buffer, cancellationToken))
			{
				yield return result;
			}

			size = Math.Min(size * 2, MaxBatchSize);
			batch = new List<IdentifyRequest>(size);
		}

		FeedRunSpans.SourceEnded(listing, pulled);
		if (batch.Count > 0 && failure?.SourceException is not OperationCanceledException)
		{
			await foreach (var result in IdentifyAndLoadAsync(batch, sink, building, connected, buffer, cancellationToken))
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
		IReadOnlySet<RemoteService> connected,
		MediaWriteBuffer buffer,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		IReadOnlyList<TracedRequest> traced = [.. batch.Select(request => new TracedRequest(request, TitleSpan(request)))];
		var handoffs = await IdentifyBatchAsync(traced, sink.AddressedBy, building, buffer, cancellationToken);
		var pipeline = traced
			.Zip(handoffs)
			.SelectAwait((pair, token) => LoadRequestDetailsAsync(pair.First, pair.Second, connected, buffer, token), RecommendedConcurrency, cancellationToken);
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

	public async Task<IReadOnlyDictionary<MediaId, MediaRatings>> GetStoredRatingsAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		var stored = await mediaRepository.GetAsync([.. ids], cancellationToken);
		return stored
			.Select(media => media.Cached.Map(cached => (media.Identity.Id, Rating: cached.Details.Ratings)))
			.WhereSome()
			.ToDictionary(entry => entry.Id, entry => entry.Rating);
	}

	public async Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetSinkIdsAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		var sink = await activeSink.ResolveAsync(cancellationToken);
		return await IdsAsync([.. ids], sink.AddressedBy, cancellationToken);
	}

	public Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetSinkIdsAsync(
		IEnumerable<MediaId> ids,
		SinkKind sink,
		CancellationToken cancellationToken = default
	) => IdsAsync([.. ids], activeSink.Of(sink).AddressedBy, cancellationToken);

	public async Task<IReadOnlyList<MediaId>> EnsureSinkIdsAsync(IEnumerable<MediaId> ids, SinkKind sink, CancellationToken cancellationToken = default)
	{
		var wanted = ids.Distinct().ToList();
		var found = await EnsureIdsAsync(wanted, activeSink.Of(sink).AddressedBy, cancellationToken);
		return [.. wanted.Where(id => !found.ContainsKey(id))];
	}

	public async Task<IReadOnlyDictionary<MediaId, MediaType>> GetTypesAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
	{
		var stored = await mediaRepository.GetAsync([.. ids.Distinct()], cancellationToken);
		return stored.ToDictionary(media => media.Identity.Id, media => media.Identity.Type);
	}

	public async Task<Optional<IExternalMediaId>> FindIdAsync(MediaId id, MediaProvider provider, CancellationToken cancellationToken = default)
	{
		try
		{
			var found = await EnsureIdsAsync([id], _ => Some.Of(provider), cancellationToken);
			return found.GetValueOrNone(id);
		}
		catch (MediaIdentityConflictException conflict)
		{
			logger.LogWarning(conflict, "The {Provider} id found for {MediaId} already belongs to another title", provider, id);
			return new None();
		}
	}

	private async Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> EnsureIdsAsync(
		IReadOnlyList<MediaId> wanted,
		Func<MediaType, Optional<MediaProvider>> addressedBy,
		CancellationToken cancellationToken
	)
	{
		var known = await IdsAsync(wanted, addressedBy, cancellationToken);
		var missing = wanted.Where(id => !known.ContainsKey(id)).ToList();
		if (missing.Count == 0)
		{
			return known;
		}

		var stored = await mediaRepository.GetAsync(missing, cancellationToken);
		IReadOnlyList<IdentifyRequest> requests = [.. stored.Select(media => new IdentifyRequest([.. media.Identity.Externals], HintsOf(media)))];
		await using (var buffer = bufferFactory.Create())
		{
			await IdentifyBatchAsync([.. requests.Select(TracedRequest.Untraced)], addressedBy, [], buffer, cancellationToken);
		}

		var found = await IdsAsync(missing, addressedBy, cancellationToken);
		return known.Concat(found).ToDictionary();
	}

	private static Optional<IdentifyHints> HintsOf(StoredMedia media)
	{
		return media.Cached.Map(cached =>
			Optional
				.Of(cached.Details.Title)
				.Map(title =>
					Optional
						.Of(cached.Details.ReleaseDate)
						.Map(released => new IdentifyHints
						{
							Title = title,
							Year = released.Year,
							MediaType = cached.Details.Type,
						})
				)
		);
	}

	private async Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> IdsAsync(
		IReadOnlyList<MediaId> wanted,
		Func<MediaType, Optional<MediaProvider>> addressedBy,
		CancellationToken cancellationToken
	)
	{
		var sources = Enum.GetValues<MediaType>().Select(addressedBy).WhereSome().ToHashSet();
		var found = new Dictionary<MediaId, IExternalMediaId>();
		foreach (var source in sources)
		{
			foreach (var (id, external) in await mediaRepository.GetExternalsAsync(wanted, source, cancellationToken))
			{
				addressedBy(external.Type).When(expected => expected == source).Apply(_ => found[id] = external);
			}
		}
		return found;
	}

	private async Task<IReadOnlyList<Result<IdentifyHandoff>>> IdentifyBatchAsync(
		IReadOnlyList<TracedRequest> requests,
		Func<MediaType, Optional<MediaProvider>> addressedBy,
		Dictionary<IExternalMediaId, MediaIdentity> building,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var allCandidates = requests.SelectMany(r => r.Request.Candidates).Distinct().ToList();
		var stored = await LoadByExternalsAsync(allCandidates, cancellationToken);
		var connected = await idResolver.ConnectedAsync(cancellationToken);
		var preliminary = await requests
			.SelectAwait((req, token) => IdentifyOneAsync(req, addressedBy, stored, connected, buffer, token), RecommendedConcurrency, cancellationToken)
			.ToListAsync(cancellationToken);
		return await coordinator.CoordinateHandoffsAsync(preliminary, building, buffer, cancellationToken);
	}

	private async ValueTask<Result<IdentifyHandoff>> IdentifyOneAsync(
		TracedRequest traced,
		Func<MediaType, Optional<MediaProvider>> addressedBy,
		IReadOnlyDictionary<IExternalMediaId, StoredMedia> stored,
		IReadOnlySet<RemoteService> connected,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		using var title = FeedRunSpans.Enter(traced.Title);
		var request = traced.Request;
		try
		{
			return await addressedBy(request.Type)
				.Match(
					target => IdentifyForAsync(request, target, stored, connected, buffer, cancellationToken),
					() => ValueTask.FromResult<Result<IdentifyHandoff>>(Error.From($"No provider addresses {request.Type} titles here."))
				);
		}
		catch (MediaIdentityConflictException conflict)
		{
			logger.LogWarning(conflict, "A title's ids belong to more than one stored title, so it stays unmatched");
			return Error.From(conflict);
		}
	}

	private ValueTask<Result<IdentifyHandoff>> IdentifyForAsync(
		IdentifyRequest request,
		MediaProvider target,
		IReadOnlyDictionary<IExternalMediaId, StoredMedia> stored,
		IReadOnlySet<RemoteService> connected,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		return FindStored(request, target, stored, buffer)
			.Apply(_ => FeedRunSpans.Known())
			.Match(found => ValueTask.FromResult<Result<IdentifyHandoff>>(Success.Of(found)), () => ResolveAsync(request, target, connected, cancellationToken));
	}

	private static Optional<IdentifyHandoff> FindStored(
		IdentifyRequest request,
		MediaProvider target,
		IReadOnlyDictionary<IExternalMediaId, StoredMedia> stored,
		MediaWriteBuffer buffer
	)
	{
		var matches = request.Candidates.Select(stored.GetValueOrNone).WhereSome().DistinctBy(r => r.Identity.Id).ToList();
		if (matches.Count > 1)
		{
			throw new MediaIdentityConflictException([.. matches.Select(m => m.Identity.Id)], [.. request.Candidates]);
		}
		return matches.FirstOrNone(match => match.Identity.Externals.OfProvider(target).IsSome()).Map(match => Matched(request, match, buffer));
	}

	private static IdentifyHandoff Matched(IdentifyRequest request, StoredMedia match, MediaWriteBuffer buffer)
	{
		var matched = match.Identity;
		var missing = request.Candidates.Where(c => !matched.Externals.Contains(c)).ToList();
		if (missing.Count == 0)
		{
			return new IdentifyHandoff(matched, Some.Of(match));
		}
		var merged = matched with { Externals = matched.Externals + missing };
		buffer.AddIdentity(merged);
		return new IdentifyHandoff(merged, Some.Of(match));
	}

	private async ValueTask<Result<IdentifyHandoff>> ResolveAsync(
		IdentifyRequest request,
		MediaProvider target,
		IReadOnlySet<RemoteService> connected,
		CancellationToken cancellationToken
	)
	{
		return await idResolver
			.ResolveAsync(request, target, connected, cancellationToken)
			.Map(externals => new IdentifyHandoff(new MediaIdentity(MediaId.New(), [.. externals]), new None()));
	}

	private async ValueTask<Result<MatchedMedia>> LoadStoredDetailsAsync(
		MediaId id,
		IReadOnlyDictionary<MediaId, StoredMedia> stored,
		IReadOnlySet<RemoteService> connected,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		if (!stored.TryGetValue(id, out var row))
		{
			return Error.From("identity not found");
		}
		return await LoadDetailsAsync(row.Identity, Some.Of(row), MediaDetails.Empty(row.Identity.Type), connected, buffer, cancellationToken);
	}

	private async ValueTask<MediaMatchResult> LoadRequestDetailsAsync(
		TracedRequest traced,
		Result<IdentifyHandoff> handoff,
		IReadOnlySet<RemoteService> connected,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		using var title = FeedRunSpans.Enter(traced.Title);
		var request = traced.Request;
		var loaded = await handoff.Match(
			match => LoadDetailsAsync(match.Identity, match.Stored, request.Described(), connected, buffer, cancellationToken),
			error => ValueTask.FromResult<Result<MatchedMedia>>(error)
		);
		var known = handoff.Match(match => match.Stored.IsSome(), _ => false);
		var result = loaded.Match<MatchedMedia, MediaMatchResult>(matched => matched with { Known = known }, error => request.Unmatched(Reason(error)));
		FeedRunSpans.Settle(traced.Title, Fate(result));
		return result;
	}

	private static Optional<Activity> TitleSpan(IdentifyRequest request)
	{
		var hints = request.Hints.Or(request.Description);
		return FeedRunSpans.Title(
			hints.Map(found => found.Title).GetValueOr(() => string.Join(", ", request.Candidates)),
			hints.Map(found => found.Year).When(year => year > 0)
		);
	}

	private static TitleFate Fate(MediaMatchResult result)
	{
		return result switch
		{
			MatchedMedia => TitleFate.Matched,
			UnmatchedMedia unmatched => Fate(unmatched.Reason),
		};
	}

	private static TitleFate Fate(UnmatchedReason reason)
	{
		return reason switch
		{
			UnmatchedReason.NotFound => TitleFate.NotFound,
			UnmatchedReason.NotFoundEarlier => TitleFate.NotFound,
			UnmatchedReason.CouldNotLookUp => TitleFate.CouldNotLookUp,
		};
	}

	private static UnmatchedReason Reason(Error error)
	{
		return error
			.CausedBy<LookupNotRunException>()
			.Map(_ => UnmatchedReason.CouldNotLookUp)
			.Or(() => error.CausedBy<NotFoundEarlierException>().Map(_ => UnmatchedReason.NotFoundEarlier))
			.GetValueOr(UnmatchedReason.NotFound);
	}

	private async ValueTask<Result<MatchedMedia>> LoadDetailsAsync(
		MediaIdentity identity,
		Optional<StoredMedia> stored,
		MediaDetails placeholder,
		IReadOnlySet<RemoteService> connected,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var cached = stored.Map(row => row.Cached);
		return await detailsFetcher
			.FindReusable(identity, cached, connected)
			.Match(
				reusable => ValueTask.FromResult<Result<MatchedMedia>>(Success.Of(new MatchedMedia(identity, reusable.Details))),
				() => FetchAsync(identity, stored, cached, placeholder, connected, buffer, cancellationToken)
			);
	}

	private async ValueTask<Result<MatchedMedia>> FetchAsync(
		MediaIdentity identity,
		Optional<StoredMedia> stored,
		Optional<CachedDetails> cached,
		MediaDetails placeholder,
		IReadOnlySet<RemoteService> connected,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		try
		{
			var fetched = await detailsFetcher.FetchAsync(identity, cached, connected, buffer, cancellationToken);
			return fetched.Match(_ => fetched, error => Fallback(identity, stored, cached, placeholder, error));
		}
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
		{
			logger.Log(stored.Match(LogLevel.Warning, LogLevel.Error), ex, "Media details failed for {MediaId}", identity.Id);
			return Fallback(identity, stored, cached, placeholder, Error.From(ex));
		}
	}

	private static Result<MatchedMedia> Fallback(
		MediaIdentity identity,
		Optional<StoredMedia> stored,
		Optional<CachedDetails> cached,
		MediaDetails placeholder,
		Error error
	)
	{
		return cached
			.Map(stale => stale.Details)
			.Or(() => stored.Map(_ => placeholder))
			.Or(() => error.CausedBy<RemoteUnavailableException>().Map(_ => placeholder))
			.Map(details => new MatchedMedia(identity, details))
			.ToResult(error);
	}

	private static Result<MediaIdentity> ToIdentityResult(Result<IdentifyHandoff> handoff) => handoff.Map(r => r.Identity);

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
		public MediaMatchResult Unmatched(UnmatchedReason reason)
		{
			return new UnmatchedMedia([.. request.Candidates], Details(request.Hints.Or(() => request.Description), request.Type), reason);
		}

		public MediaDetails Described()
		{
			return Details(request.Hints, request.Type);
		}
	}

	private static MediaDetails Details(Optional<IdentifyHints> hints, MediaType type)
	{
		return hints.Map(Hinted).GetValueOr(() => MediaDetails.Empty(type));
	}

	private static MediaDetails Hinted(IdentifyHints hints)
	{
		return MediaDetails.Empty(hints.MediaType) with { Title = hints.Title, ReleaseDate = hints.Year > 0 ? new DateOnly(hints.Year, 1, 1) : null };
	}
}
