using Contracts.Media;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal sealed class MediaWriteBufferFactory(IMediaRepository mediaRepository, ILogger<MediaWriteBuffer> logger)
{
	public MediaWriteBuffer Create() => new(mediaRepository, logger);
}
