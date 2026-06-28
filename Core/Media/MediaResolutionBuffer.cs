using System.Collections.Concurrent;
using System.Diagnostics;
using Contracts.Media;
using Shared.Telemetry;

namespace Core.Media;

internal sealed class MediaResolutionBuffer : IAsyncDisposable
{
	private readonly IMediaRepository _mediaRepository;
	private readonly Activity? _activity;

	public ConcurrentBag<MediaIdentity> NewIdentities { get; } = new();
	public ConcurrentBag<IdentifiedMedia> NewMetadata { get; } = new();

	public MediaResolutionBuffer(IMediaRepository mediaRepository)
	{
		_mediaRepository = mediaRepository;
		_activity = SammlarrTelemetry.StartMediaFlush();
	}

	public void AddIdentity(MediaIdentity identity) => NewIdentities.Add(identity);

	public void Add(MediaLookup detail)
	{
		var stored = detail.AsIdentified();
		if (stored is null)
		{
			return;
		}
		NewIdentities.Add(stored.Value.Identity);
		NewMetadata.Add(stored.Value);
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
				var canonical = await _mediaRepository.UpsertIdentitiesAsync(identities, CancellationToken.None);
				metadata = RemapMetadata(metadata, identities, canonical);
			}
			if (metadata.Count > 0)
			{
				await _mediaRepository.UpsertMetadataAsync(metadata, CancellationToken.None);
			}
		}
		finally
		{
			_activity?.Dispose();
		}
	}

	private static List<IdentifiedMedia> RemapMetadata(List<IdentifiedMedia> metadata, List<MediaIdentity> identities, IReadOnlyList<MediaIdentity> canonical)
	{
		var remap = new Dictionary<Guid, Guid>();
		for (var i = 0; i < identities.Count && i < canonical.Count; i++)
		{
			remap[identities[i].Id.Value] = canonical[i].Id.Value;
		}
		return [.. metadata.Select(item => remap.TryGetValue(item.Id.Value, out var id) ? item.WithIdentity(item.Identity with { Id = MediaId.From(id) }) : item)];
	}
}
