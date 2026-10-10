using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Contracts.Media;
using Contracts.Media.Conversions;
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
	IIdentityClaims claims,
	MediaWriteBufferFactory bufferFactory,
	ActiveSink activeSink,
	IMediaRepository mediaRepository,
	ILogger<MediaService> logger
) : IMediaService
{
	internal const int RecommendedConcurrency = 100;
	internal const int FirstBatchSize = 10;
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

		var sink = await activeSink.ResolveAsync(cancellationToken);
		var run = await TitleRun.ForIdsAsync(sink.AddressedBy, idResolver, cancellationToken);
		var handoffs = await IdentifyBatchAsync([.. materialized.Select(TracedRequest.Untraced)], run, cancellationToken);
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
		var run = await TitleRun.StartAsync(activeSink, idResolver, detailsFetcher, cancellationToken);
		var size = FirstBatchSize;
		var batch = new List<IdentifyRequest>(size);
		await using var source = FeedRunSpans
			.SourceAsync(requests.PrefetchAsync(PrefetchCapacity, cancellationToken), cancellationToken)
			.GetAsyncEnumerator(cancellationToken);
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

			await foreach (var result in IdentifyAndLoadAsync(batch, run, buffer, cancellationToken))
			{
				yield return result;
			}

			size = Math.Min(size * 2, MaxBatchSize);
			batch = new List<IdentifyRequest>(size);
		}

		if (batch.Count > 0 && failure?.SourceException is not OperationCanceledException)
		{
			await foreach (var result in IdentifyAndLoadAsync(batch, run, buffer, cancellationToken))
			{
				yield return result;
			}
		}

		failure?.Throw();
	}

	private async IAsyncEnumerable<MediaMatchResult> IdentifyAndLoadAsync(
		IReadOnlyList<IdentifyRequest> batch,
		TitleRun run,
		MediaWriteBuffer buffer,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		IReadOnlyList<TracedRequest> traced = [.. batch.Select(TitleRun.Traced)];
		var handoffs = await IdentifyBatchAsync(traced, run, cancellationToken);
		var pipeline = traced
			.Zip(handoffs)
			.SelectAwait(
				(pair, token) => LoadRequestDetailsAsync(pair.First, pair.Second, run.DetailServices, buffer, token),
				RecommendedConcurrency,
				cancellationToken
			);
		await foreach (var result in pipeline)
		{
			yield return result;
		}
	}

	public async Task<IReadOnlyList<Optional<MediaIdentity>>> RegisterAsync(
		IReadOnlyList<IReadOnlyList<IExternalMediaId>> candidateGroups,
		ValueSet<RemoteService> sources,
		CancellationToken cancellationToken = default
	)
	{
		var outcomes = await Task.WhenAll(
			candidateGroups.Select(group => claims.ClaimAsync(new IdentityClaim([.. group.OfType<IIdentityKey>()], new None(), sources), cancellationToken))
		);
		return [.. outcomes.Select(Registered)];
	}

	private static Optional<MediaIdentity> Registered(ClaimOutcome outcome)
	{
		return outcome switch
		{
			Claimed claimed => Some.Of(new MediaIdentity(claimed.Id, claimed.Keys)),
			Conflicted => new None(),
		};
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
		return provider.Role switch
		{
			ExternalIdRole.IdentityKey => await FindKeyAsync(id, provider, cancellationToken),
			ExternalIdRole.PageLink => await FindPageLinkAsync(id, provider, cancellationToken),
		};
	}

	private async Task<Optional<IExternalMediaId>> FindPageLinkAsync(MediaId id, MediaProvider site, CancellationToken cancellationToken)
	{
		var stored = await mediaRepository.GetAsync([id], cancellationToken);
		return await stored
			.FirstOrNone()
			.Match(media => HeldOrAskedAsync(media, site, cancellationToken), () => Task.FromResult<Optional<IExternalMediaId>>(new None()));
	}

	private Task<Optional<IExternalMediaId>> HeldOrAskedAsync(StoredMedia media, MediaProvider site, CancellationToken cancellationToken)
	{
		return media
			.Cached.Map(cached => cached.Details.PageLinks.FirstOrNone(link => link.Source == site))
			.Match(held => Task.FromResult<Optional<IExternalMediaId>>(Some.Of<IExternalMediaId>(held)), () => AskPageLinkAsync(media, site, cancellationToken));
	}

	private async Task<Optional<IExternalMediaId>> AskPageLinkAsync(StoredMedia media, MediaProvider site, CancellationToken cancellationToken)
	{
		var connected = await idResolver.ConnectedAsync(cancellationToken);
		var resolved = await idResolver.ResolveAsync(new IdentifyRequest([.. media.Identity.Keys], HintsOf(media)), site, connected, cancellationToken);
		var found = resolved.GetValueOr([]);
		List<IPageLink> links = [.. found.OfType<IPageLink>()];
		List<IIdentityKey> learned = [.. found.OfType<IIdentityKey>().Where(key => !media.Identity.Keys.Contains(key))];
		if (links.Count > 0)
		{
			await mediaRepository.SavePageLinksAsync(media.Identity.Id, links, cancellationToken);
		}

		if (learned.Count > 0)
		{
			await claims.ClaimAsync(new IdentityClaim([.. learned], Some.Of(media.Identity.Id), []), cancellationToken);
		}

		return links.FirstOrNone(link => link.Source == site).Map(link => (IExternalMediaId)link);
	}

	private async Task<Optional<IExternalMediaId>> FindKeyAsync(MediaId id, MediaProvider provider, CancellationToken cancellationToken)
	{
		var found = await EnsureIdsAsync([id], _ => Some.Of(provider), cancellationToken);
		return found.GetValueOrNone(id);
	}

	public Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetIdsAsync(
		IEnumerable<MediaId> ids,
		MediaProvider provider,
		CancellationToken cancellationToken = default
	) => mediaRepository.GetExternalsAsync(ids, provider, cancellationToken);

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
		IReadOnlyList<IdentifyRequest> requests = [.. stored.Select(media => new IdentifyRequest([.. media.Identity.Keys], HintsOf(media)))];
		var run = await TitleRun.ForIdsAsync(addressedBy, idResolver, cancellationToken);
		await IdentifyBatchAsync([.. requests.Select(TracedRequest.Untraced)], run, cancellationToken);

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
		TitleRun run,
		CancellationToken cancellationToken
	)
	{
		var allCandidates = requests.SelectMany(r => r.Request.Candidates).Distinct().ToList();
		var stored = await LoadByExternalsAsync(allCandidates, cancellationToken);
		return await requests
			.SelectAwait((req, token) => IdentifyOneAsync(req, run.AddressedBy, stored, run.IdServices, token), RecommendedConcurrency, cancellationToken)
			.ToListAsync(cancellationToken);
	}

	private async ValueTask<Result<IdentifyHandoff>> IdentifyOneAsync(
		TracedRequest traced,
		Func<MediaType, Optional<MediaProvider>> addressedBy,
		IReadOnlyDictionary<IExternalMediaId, StoredMedia> stored,
		IReadOnlySet<RemoteService> connected,
		CancellationToken cancellationToken
	)
	{
		using var title = FeedRunSpans.Enter(traced.Title);
		var request = traced.Request;
		return await addressedBy(request.Type)
			.Match(
				target => IdentifyForAsync(request, target, stored, connected, cancellationToken),
				() => ValueTask.FromResult<Result<IdentifyHandoff>>(Error.From($"No provider addresses {request.Type} titles here."))
			);
	}

	private async ValueTask<Result<IdentifyHandoff>> IdentifyForAsync(
		IdentifyRequest request,
		MediaProvider target,
		IReadOnlyDictionary<IExternalMediaId, StoredMedia> stored,
		IReadOnlySet<RemoteService> connected,
		CancellationToken cancellationToken
	)
	{
		List<StoredMedia> matches = [.. request.Candidates.Select(stored.GetValueOrNone).WhereSome().DistinctBy(match => match.Identity.Id)];
		if (matches.Count > 1)
		{
			return Conflict(request, [.. matches.Select(match => match.Identity.Id)]);
		}

		return await matches
			.FirstOrNone(match => match.Identity.Keys.OfProvider(target).IsSome())
			.Apply(_ => FeedRunSpans.Known())
			.Match(
				match => ExtendStoredAsync(request, match, cancellationToken),
				() => ResolveAsync(request, target, matches.FirstOrNone(), connected, cancellationToken)
			);
	}

	private async ValueTask<Result<IdentifyHandoff>> ExtendStoredAsync(IdentifyRequest request, StoredMedia match, CancellationToken cancellationToken)
	{
		List<IIdentityKey> missing = [.. request.Candidates.OfType<IIdentityKey>().Where(key => !match.Identity.Keys.Contains(key))];
		if (missing.Count == 0)
		{
			return Success.Of(new IdentifyHandoff(match.Identity, Some.Of(match)));
		}

		var outcome = await claims.ClaimAsync(new IdentityClaim([.. missing], Some.Of(match.Identity.Id), request.Sources), cancellationToken);
		return Handoff(request, outcome, Some.Of(match));
	}

	private async ValueTask<Result<IdentifyHandoff>> ResolveAsync(
		IdentifyRequest request,
		MediaProvider target,
		Optional<StoredMedia> stored,
		IReadOnlySet<RemoteService> connected,
		CancellationToken cancellationToken
	)
	{
		var resolved = await idResolver.ResolveAsync(request, target, connected, cancellationToken);
		return await resolved.Match(
			ids => ClaimAsync(request, ids, stored.Map(match => match.Identity.Id), cancellationToken),
			error => Task.FromResult<Result<IdentifyHandoff>>(error)
		);
	}

	private async Task<Result<IdentifyHandoff>> ClaimAsync(
		IdentifyRequest request,
		IReadOnlyList<IExternalMediaId> ids,
		Optional<MediaId> pinned,
		CancellationToken cancellationToken
	)
	{
		var outcome = await claims.ClaimAsync(new IdentityClaim([.. ids.OfType<IIdentityKey>()], pinned, request.Sources), cancellationToken);
		return Handoff(request, outcome, new None());
	}

	private static Result<IdentifyHandoff> Handoff(IdentifyRequest request, ClaimOutcome outcome, Optional<StoredMedia> stored)
	{
		return outcome switch
		{
			Claimed claimed => Success.Of(
				new IdentifyHandoff(new MediaIdentity(claimed.Id, stored.Map(match => match.Identity.Keys).GetValueOr([]) + claimed.Keys), stored)
			),
			Conflicted conflicted => Conflict(request, conflicted.Owners),
		};
	}

	private static Result<IdentifyHandoff> Conflict(IdentifyRequest request, ValueSet<MediaId> owners)
	{
		return Error.From(new MediaIdentityConflictException([.. owners], [.. request.Candidates]));
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
		var result = loaded.Match<MatchedMedia, MediaMatchResult>(matched => matched with { Known = known }, error => Unmatched(request, error));
		FeedRunSpans.Settle(traced.Title, Fate(result));
		return result;
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
			UnmatchedReason.Conflict => TitleFate.Conflict,
		};
	}

	private static UnmatchedMedia Unmatched(IdentifyRequest request, Error error)
	{
		return error
			.CausedBy<MediaIdentityConflictException>()
			.Match(conflict => request.Unmatched(UnmatchedReason.Conflict) with { ConflictsWith = [.. conflict.MediaIds] }, () => request.Unmatched(Reason(error)));
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
		return rows.SelectMany(r => r.Identity.Keys.Select(e => (External: (IExternalMediaId)e, Row: r))).ToDictionary(t => t.External, t => t.Row);
	}
}

file static class IdentifyRequestExtensions
{
	extension(IdentifyRequest request)
	{
		public UnmatchedMedia Unmatched(UnmatchedReason reason)
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
