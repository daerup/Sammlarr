namespace Contracts.Media;

public interface IMediaCachePurger
{
	Task PurgeAsync(CancellationToken cancellationToken = default);

	Task PurgeDetailsAsync(CancellationToken cancellationToken = default);
}
