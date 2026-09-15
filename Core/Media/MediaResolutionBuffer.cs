using System.Collections.Concurrent;
using Contracts.Media;
using Domain.Media;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal sealed class MediaResolutionBuffer : IAsyncDisposable
{
	private readonly IMediaRepository _mediaRepository;
	private readonly ILogger<MediaResolutionBuffer> _logger;
	private readonly ConcurrentDictionary<MediaId, MediaIdentity> _identities = new();
	private readonly ConcurrentDictionary<MediaId, MediaId> _absorbed = new();

	public IReadOnlyCollection<MediaIdentity> NewIdentities => [.. _identities.Values];
	public ConcurrentBag<IdentifiedMedia> NewMetadata { get; } = new();

	public MediaResolutionBuffer(IMediaRepository mediaRepository, ILogger<MediaResolutionBuffer> logger)
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

	public void Add(MediaLookup detail)
	{
		var stored = detail.AsIdentified();
		if (stored is null)
		{
			return;
		}
		AddIdentity(stored.Value.Identity);
		NewMetadata.Add(stored.Value);
	}

	public async ValueTask DisposeAsync()
	{
		var identities = _identities.Values.ToList();
		var metadata = NewMetadata.ToList();
		if (identities.Count > 0)
		{
			var canonical = await _mediaRepository.UpsertIdentitiesAsync(identities, CancellationToken.None);
			metadata = RemapMetadata(metadata, identities, canonical);
		}
		if (metadata.Count > 0)
		{
			await _mediaRepository.UpsertMetadataAsync(metadata, CancellationToken.None);
		}
		if (identities.Count > 0 || metadata.Count > 0)
		{
			_logger.LogDebug("Media resolution buffer flushed: upserted {Identities} identities, {Metadata} metadata rows", identities.Count, metadata.Count);
		}
	}

	private List<IdentifiedMedia> RemapMetadata(List<IdentifiedMedia> metadata, List<MediaIdentity> identities, IReadOnlyList<MediaIdentity> canonical)
	{
		var upserted = new Dictionary<MediaId, MediaId>();
		for (var i = 0; i < identities.Count && i < canonical.Count; i++)
		{
			upserted[identities[i].Id] = canonical[i].Id;
		}
		return [.. metadata.Select(item => Remap(item, Survivor(item.Id, upserted)))];
	}

	private MediaId Survivor(MediaId id, IReadOnlyDictionary<MediaId, MediaId> upserted)
	{
		while (_absorbed.TryGetValue(id, out var next))
		{
			id = next;
		}
		return upserted.GetValueOrDefault(id, id);
	}

	private static IdentifiedMedia Remap(IdentifiedMedia item, MediaId survivor) =>
		survivor == item.Id ? item : item.WithIdentity(item.Identity with { Id = survivor });
}
