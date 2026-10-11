using Contracts.Media;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal sealed class LibraryWriterFactory(IMediaRepository repository, ILogger<LibraryWriter> logger)
{
	public LibraryWriter Create() => new(repository, logger);
}
