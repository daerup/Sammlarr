using Contracts.Media;
using Domain.Media;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal sealed class MediaWriteBuffer : IAsyncDisposable
{
	private readonly IMediaRepository _mediaRepository;
	private readonly ILogger<MediaWriteBuffer> _logger;
	private readonly Lock _gate = new();
	private readonly Dictionary<MediaId, CachedDetails> _details = [];

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

	public void Add(MediaId id, CachedDetails cached)
	{
		lock (_gate)
		{
			_details[id] = cached;
		}
	}

	public async ValueTask DisposeAsync()
	{
		Dictionary<MediaId, CachedDetails> details;
		lock (_gate)
		{
			details = new Dictionary<MediaId, CachedDetails>(_details);
		}
		if (details.Count > 0)
		{
			await _mediaRepository.UpsertDetailsAsync(details, CancellationToken.None);
			_logger.LogDebug("Media write buffer flushed: upserted {Details} details rows", details.Count);
		}
	}
}
