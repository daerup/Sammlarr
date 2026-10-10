using Contracts.Media;
using Domain.Media;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal sealed class MediaWriteBuffer : IAsyncDisposable
{
	private readonly IMediaRepository _mediaRepository;
	private readonly ILogger<MediaWriteBuffer> _logger;
	private readonly Lock _gate = new();
	private readonly Dictionary<MediaId, MediaIdentity> _identities = [];
	private readonly Dictionary<MediaId, MediaId> _absorbed = [];
	private readonly Dictionary<MediaId, CachedDetails> _details = [];

	public IReadOnlyCollection<MediaIdentity> NewIdentities
	{
		get
		{
			lock (_gate)
			{
				return [.. _identities.Values];
			}
		}
	}

	public IReadOnlyDictionary<MediaId, CachedDetails> NewDetails
	{
		get
		{
			lock (_gate)
			{
				return new Dictionary<MediaId, CachedDetails>(_details);
			}
		}
	}

	public MediaWriteBuffer(IMediaRepository mediaRepository, ILogger<MediaWriteBuffer> logger)
	{
		_mediaRepository = mediaRepository;
		_logger = logger;
	}

	public void AddIdentity(MediaIdentity identity)
	{
		lock (_gate)
		{
			var survivor = Survivor(identity.Id);
			_identities[survivor] = _identities
				.GetValueOrNone(survivor)
				.Map(existing => existing with { Keys = existing.Keys + identity.Keys })
				.GetValueOr(() => identity with { Id = survivor });
		}
	}

	public void MergeIdentity(MediaId absorbed, MediaIdentity survivor)
	{
		lock (_gate)
		{
			var from = Survivor(absorbed);
			var to = Survivor(survivor.Id);
			var merged = survivor with { Id = to };
			if (from != to)
			{
				merged = _identities.GetValueOrNone(from).Map(found => merged with { Keys = merged.Keys + found.Keys }).GetValueOr(merged);
				_absorbed[from] = to;
				_identities.Remove(from);
			}

			AddIdentity(merged);
		}
	}

	public void Add(MatchedMedia media, CachedDetails cached)
	{
		lock (_gate)
		{
			AddIdentity(media.Identity);
			_details[Survivor(media.Identity.Id)] = cached;
		}
	}

	internal MediaId Survivor(MediaId id)
	{
		lock (_gate)
		{
			while (_absorbed.TryGetValue(id, out var next))
			{
				id = next;
			}
			return id;
		}
	}

	public async ValueTask DisposeAsync()
	{
		List<MediaIdentity> identities;
		Dictionary<MediaId, CachedDetails> details;
		lock (_gate)
		{
			identities = [.. _identities.Values];
			details = new Dictionary<MediaId, CachedDetails>(_details);
		}
		if (identities.Count > 0)
		{
			var canonical = await _mediaRepository.UpsertIdentitiesAsync(identities, CancellationToken.None);
			details = RemapDetails(details, identities, canonical);
		}
		if (details.Count > 0)
		{
			await _mediaRepository.UpsertDetailsAsync(details, CancellationToken.None);
		}
		if (identities.Count > 0 || details.Count > 0)
		{
			_logger.LogDebug("Media write buffer flushed: upserted {Identities} identities, {Details} details rows", identities.Count, details.Count);
		}
	}

	private Dictionary<MediaId, CachedDetails> RemapDetails(
		Dictionary<MediaId, CachedDetails> details,
		List<MediaIdentity> identities,
		IReadOnlyList<MediaIdentity> canonical
	)
	{
		var upserted = new Dictionary<MediaId, MediaId>();
		for (var i = 0; i < identities.Count && i < canonical.Count; i++)
		{
			upserted[identities[i].Id] = canonical[i].Id;
		}
		var remapped = new Dictionary<MediaId, CachedDetails>();
		foreach (var (id, cached) in details)
		{
			var survivor = Survivor(id);
			remapped[upserted.GetValueOrDefault(survivor, survivor)] = cached;
		}
		return remapped;
	}
}
