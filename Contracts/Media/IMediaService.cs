using Contracts.Media.Conversions;
using Domain.Media;

namespace Contracts.Media;

public interface IMediaService
{
	IAsyncEnumerable<Result<MediaIdentity>> IdentifyAsync(IEnumerable<ResolveRequest> requests, CancellationToken cancellationToken = default);

	IAsyncEnumerable<Result<IdentifiedMedia>> GetDetailsAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	IAsyncEnumerable<TMedia> GetDetailsAsync<TMedia>(IEnumerable<IListItem<TMedia>> items, CancellationToken cancellationToken = default);

	IAsyncEnumerable<MediaLookup> IdentifyWithDetailsAsync(IAsyncEnumerable<ResolveRequest> requests, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaIdentity>> RegisterAsync(
		IReadOnlyList<IReadOnlyList<IExternalMediaId>> candidateGroups,
		CancellationToken cancellationToken = default
	);

	Task InvalidateAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetSinkIdsAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);
}

public record ResolveRequest(IReadOnlyList<IExternalMediaId> Candidates, Optional<ResolutionHints> Hints)
{
	public MediaType Type => Candidates.Count == 0 ? MediaType.Unknown : Candidates[0].Type;
}
