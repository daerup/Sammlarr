using Contracts.Media.Convertions;

namespace Contracts.Media;

public interface IMediaService
{
	IAsyncEnumerable<Result<Identification>> IdentifyAsync(IEnumerable<ResolveRequest> requests, CancellationToken cancellationToken = default);

	IAsyncEnumerable<Result<MediaLookup>> GetDetailsAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);

	IAsyncEnumerable<Result<MediaLookup>> GetDetailsAsync(IEnumerable<ResolveRequest> requests, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaIdentity>> ResolveIdentitiesAsync(
		IReadOnlyList<IReadOnlyList<IExternalMediaId>> candidateGroups,
		CancellationToken cancellationToken = default
	);

	Task InvalidateAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);
}

public record ResolveRequest(IReadOnlyList<IExternalMediaId> Candidates, MediaProvider Target, Optional<ResolutionHints> Hints);
