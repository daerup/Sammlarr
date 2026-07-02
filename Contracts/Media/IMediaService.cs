using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;

namespace Contracts.Media;

public interface IMediaService
{
	IAsyncEnumerable<Result<MediaIdentity>> IdentifyAsync(IEnumerable<ResolveRequest> requests, CancellationToken cancellationToken = default);

	IAsyncEnumerable<Result<IdentifiedMedia>> GetDetailsAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);

	IAsyncEnumerable<MediaLookup> ResolveDetailsAsync(IEnumerable<ResolveRequest> requests, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaIdentity>> ResolveIdentitiesAsync(
		IReadOnlyList<IReadOnlyList<IExternalMediaId>> candidateGroups,
		CancellationToken cancellationToken = default
	);

	Task InvalidateAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);
}

public record ResolveRequest(IReadOnlyList<IExternalMediaId> Candidates, MediaProvider Target, Optional<ResolutionHints> Hints);
