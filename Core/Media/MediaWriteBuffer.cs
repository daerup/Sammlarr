using System.Collections.Concurrent;
using Contracts.Media;
using Domain.Media;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal sealed class MediaWriteBuffer : IAsyncDisposable
{
	private readonly IMediaRepository _mediaRepository;
	private readonly ILogger<MediaWriteBuffer> _logger;
	private readonly ConcurrentDictionary<MediaId, MediaIdentity> _identities = new();
	private readonly ConcurrentDictionary<MediaId, MediaId> _absorbed = new();
	private readonly ConcurrentDictionary<MediaId, CachedDetails> _details = new();

	public IReadOnlyCollection<MediaIdentity> NewIdentities => [.. _identities.Values];
	public IReadOnlyDictionary<MediaId, CachedDetails> NewDetails => _details;

	public MediaWriteBuffer(IMediaRepository mediaRepository, ILogger<MediaWriteBuffer> logger)
	{
		_mediaRepository = mediaRepository;
		_logger = logger;
	}

	public void AddIdentity(MediaIdentity identity) =>
		_identities.AddOrUpdate(identity.Id, identity, (_, existing) => existing with { Externals = existing.Externals + identity.Externals });

	public void MergeIdentity(MediaId absorbed, MediaIdentity survivor)
	{
		_identities.TryRemove(absorbed, out _);
		_absorbed[absorbed] = survivor.Id;
		AddIdentity(survivor);
	}

	public void Add(MatchedMedia media, CachedDetails cached)
	{
		AddIdentity(media.Identity);
		_details[media.Identity.Id] = cached;
	}

	public async ValueTask DisposeAsync()
	{
		var identities = _identities.Values.ToList();
		var details = new Dictionary<MediaId, CachedDetails>(_details);
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
			remapped[Survivor(id, upserted)] = cached;
		}
		return remapped;
	}

	private MediaId Survivor(MediaId id, IReadOnlyDictionary<MediaId, MediaId> upserted)
	{
		while (_absorbed.TryGetValue(id, out var next))
		{
			id = next;
		}
		return upserted.GetValueOrDefault(id, id);
	}
}
