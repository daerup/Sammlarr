using System.Runtime.CompilerServices;
using Contracts.Media;
using Core.Wantlist.Sync;
using Domain.Media;
using Domain.Providers;

namespace Core.Media;

internal sealed class MediaService(
	MediaIdResolver idResolver,
	TitleIdentifier identifier,
	DetailsFetcher detailsFetcher,
	ListLoad listLoad,
	IIdentityClaims claims,
	MediaWriteBufferFactory bufferFactory,
	ActiveSink activeSink,
	IMediaRepository mediaRepository
) : IMediaService
{
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
		foreach (var identification in await IdentifyBatchAsync(materialized, sink.AddressedBy, cancellationToken))
		{
			yield return ToIdentityResult(identification);
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
		using var abandon = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
		List<Task<Result<MatchedMedia>>> started = [.. materialized.Select(id => LoadStoredDetailsAsync(id, stored, connected, buffer, abandon.Token))];
		try
		{
			foreach (var loading in started)
			{
				yield return await loading;
			}
		}
		finally
		{
			await abandon.CancelAsync();
			await ((Task)Task.WhenAll(started)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
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

	public IAsyncEnumerable<Listed<MediaMatchResult>> LoadListAsync(IAsyncEnumerable<ListTitle> entries, CancellationToken cancellationToken = default)
	{
		return listLoad.LoadAsync(entries, cancellationToken);
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
		var resolution = await idResolver.ResolveAsync(new IdentifyRequest([.. media.Identity.Keys], media.Hints()), site, connected, cancellationToken);
		IReadOnlyList<IExternalMediaId> found = resolution switch
		{
			Resolved resolved => resolved.Ids,
			NoMatchAnywhere or NotFoundEarlier or NotRun => [],
		};
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
		IReadOnlyList<IdentifyRequest> requests = [.. stored.Select(media => new IdentifyRequest([.. media.Identity.Keys], media.Hints()))];
		await IdentifyBatchAsync(requests, addressedBy, cancellationToken);

		var found = await IdsAsync(missing, addressedBy, cancellationToken);
		return known.Concat(found).ToDictionary();
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

	private async Task<IReadOnlyList<Identification>> IdentifyBatchAsync(
		IReadOnlyList<IdentifyRequest> requests,
		Func<MediaType, Optional<MediaProvider>> addressedBy,
		CancellationToken cancellationToken
	)
	{
		var connected = await idResolver.ConnectedAsync(cancellationToken);
		var stored = await LoadByExternalsAsync([.. requests.SelectMany(request => request.Candidates).Distinct()], cancellationToken);
		await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
		return await Task.WhenAll(requests.Select(request => IdentifyOneAsync(request, addressedBy, stored, connected, cancellationToken)));
	}

	private Task<Identification> IdentifyOneAsync(
		IdentifyRequest request,
		Func<MediaType, Optional<MediaProvider>> addressedBy,
		IReadOnlyDictionary<IExternalMediaId, StoredMedia> stored,
		IReadOnlySet<RemoteService> connected,
		CancellationToken cancellationToken
	)
	{
		IReadOnlyList<StoredMedia> rows = [.. request.Candidates.Select(stored.GetValueOrNone).WhereSome().DistinctBy(row => row.Identity.Id)];
		return addressedBy(request.Type)
			.Match(
				target => identifier.IdentifyAsync(request, target, rows, connected, cancellationToken),
				() => Task.FromResult<Identification>(new Unidentified(UnmatchedReason.NotFound, []))
			);
	}

	private Task<Result<MatchedMedia>> LoadStoredDetailsAsync(
		MediaId id,
		IReadOnlyDictionary<MediaId, StoredMedia> stored,
		IReadOnlySet<RemoteService> connected,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		return stored
			.GetValueOrNone(id)
			.Match(
				row =>
					detailsFetcher.LoadAsync(
						new IdentifyHandoff(row.Identity, Some.Of(row)),
						MediaDetails.Empty(row.Identity.Type),
						connected,
						buffer,
						cancellationToken
					),
				() => Task.FromResult<Result<MatchedMedia>>(Error.From("identity not found"))
			);
	}

	private static Result<MediaIdentity> ToIdentityResult(Identification identification)
	{
		return identification switch
		{
			IdentifyHandoff handoff => Success.Of(handoff.Identity),
			Unidentified unidentified => Error.From($"The title stays unidentified ({unidentified.Reason})."),
		};
	}

	private async Task<IReadOnlyDictionary<MediaId, StoredMedia>> LoadByInternalsAsync(IReadOnlyList<MediaId> ids, CancellationToken cancellationToken)
	{
		var rows = await mediaRepository.GetAsync(ids, cancellationToken);
		return rows.ToDictionary(row => row.Identity.Id);
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
		return rows.SelectMany(row => row.Identity.Keys.Select(key => (Key: (IExternalMediaId)key, Row: row))).ToDictionary(pair => pair.Key, pair => pair.Row);
	}
}
