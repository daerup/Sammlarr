using Contracts.Media;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal sealed class MediaResolutionBufferFactory(IMediaRepository mediaRepository, ILogger<MediaResolutionBuffer> logger)
{
	public MediaResolutionBuffer Create() => new(mediaRepository, logger);
}
