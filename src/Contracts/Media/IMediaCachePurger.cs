namespace Contracts.Media;

public interface IMediaCachePurger
{
	Task<CachedTitles> CountAsync(CancellationToken cancellationToken = default);

	Task PurgeAsync(CancellationToken cancellationToken = default);

	Task PurgeDetailsAsync(CancellationToken cancellationToken = default);

	Task PurgeShowPartsAsync(CancellationToken cancellationToken = default);
}

public sealed record CachedTitles(int Detailed, long DetailsBytes, int Matched, long IdsBytes, int ShowParts, long ShowPartsBytes);
