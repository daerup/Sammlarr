namespace Contracts.Media.Metadata;

public interface IMediaCachePurger
{
	Task PurgeAsync(CancellationToken cancellationToken = default);
}
