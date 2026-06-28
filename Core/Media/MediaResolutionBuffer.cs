using System.Collections.Concurrent;
using System.Diagnostics;
using Contracts.Media;
using Contracts.Media.Metadata;
using Shared.Telemetry;

namespace Core.Media;

internal sealed class MediaResolutionBuffer : IAsyncDisposable
{
	private readonly IMediaRepository _mediaRepository;
	private readonly IMediaMetadataRepository _metadataRepository;
	private readonly Activity? _activity;

	public ConcurrentBag<MediaIdentity> NewIdentities { get; } = new();
	public ConcurrentBag<MediaMetadataUpsert> NewMetadata { get; } = new();

	public MediaResolutionBuffer(IMediaRepository mediaRepository, IMediaMetadataRepository metadataRepository)
	{
		_mediaRepository = mediaRepository;
		_metadataRepository = metadataRepository;
		_activity = SammlarrTelemetry.StartMediaFlush();
	}

	public void AddIdentity(MediaIdentity identity) => NewIdentities.Add(identity);

	public void Add(MediaMatch detail)
	{
		if (detail is UnmatchedMedia)
		{
			return;
		}
		var identity = detail.Identity;
		NewIdentities.Add(identity);
		NewMetadata.Add(new MediaMetadataUpsert(identity.Id, detail));
	}

	public async ValueTask DisposeAsync()
	{
		var identities = NewIdentities.ToList();
		var metadata = NewMetadata.ToList();
		SammlarrTelemetry.TagMediaFlushCounts(_activity, identities.Count, metadata.Count);
		try
		{
			if (identities.Count > 0)
			{
				var canonical = await _mediaRepository.UpsertManyAsync(identities, CancellationToken.None);
				metadata = RemapMetadata(metadata, identities, canonical);
			}
			if (metadata.Count > 0)
			{
				await _metadataRepository.UpsertManyAsync(metadata, CancellationToken.None);
			}
		}
		finally
		{
			_activity?.Dispose();
		}
	}

	private static List<MediaMetadataUpsert> RemapMetadata(
		List<MediaMetadataUpsert> metadata,
		List<MediaIdentity> identities,
		IReadOnlyList<MediaIdentity> canonical
	)
	{
		var remap = new Dictionary<Guid, Guid>();
		for (var i = 0; i < identities.Count && i < canonical.Count; i++)
		{
			remap[identities[i].Id.Value] = canonical[i].Id.Value;
		}
		return [.. metadata.Select(item => remap.TryGetValue(item.MediaId.Value, out var id) ? item with { MediaId = MediaId.From(id) } : item)];
	}
}
