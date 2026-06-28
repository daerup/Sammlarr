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
		SammlarrTelemetry.TagMediaFlushCounts(_activity, NewIdentities.Count, NewMetadata.Count);
		try
		{
			if (!NewIdentities.IsEmpty)
			{
				await _mediaRepository.UpsertManyAsync(NewIdentities.ToList(), CancellationToken.None);
			}
			if (!NewMetadata.IsEmpty)
			{
				await _metadataRepository.UpsertManyAsync(NewMetadata.ToList(), CancellationToken.None);
			}
		}
		finally
		{
			_activity?.Dispose();
		}
	}
}
