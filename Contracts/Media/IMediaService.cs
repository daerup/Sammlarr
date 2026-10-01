using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;

namespace Contracts.Media;

public interface IMediaService
{
	IAsyncEnumerable<Result<MediaIdentity>> IdentifyAsync(IEnumerable<ResolveRequest> requests, CancellationToken cancellationToken = default);

	IAsyncEnumerable<Result<IdentifiedMedia>> GetDetailsAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);

	IAsyncEnumerable<TMedia> GetDetailsAsync<TMedia>(IEnumerable<IListItem<TMedia>> items, CancellationToken cancellationToken = default);

	IAsyncEnumerable<MediaLookup> ResolveDetailsAsync(IAsyncEnumerable<ResolveRequest> requests, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaIdentity>> ResolveIdentitiesAsync(
		IReadOnlyList<IReadOnlyList<IExternalMediaId>> candidateGroups,
		CancellationToken cancellationToken = default
	);

	Task InvalidateAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetExternalsAsync(
		IEnumerable<MediaId> mediaIds,
		MediaProvider source,
		CancellationToken cancellationToken = default
	);
}

public record ResolveRequest(IReadOnlyList<IExternalMediaId> Candidates, MediaProvider Target, Optional<ResolutionHints> Hints);
