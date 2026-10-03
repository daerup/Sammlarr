using Contracts.Media.Conversions;
using Domain.Media;

namespace Contracts.Media;

public interface IMediaService
{
	IAsyncEnumerable<Result<MediaIdentity>> IdentifyAsync(IEnumerable<IdentifyRequest> requests, CancellationToken cancellationToken = default);

	IAsyncEnumerable<Result<MatchedMedia>> GetDetailsAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	IAsyncEnumerable<TMedia> GetDetailsAsync<TMedia>(IEnumerable<IListItem<TMedia>> items, CancellationToken cancellationToken = default);

	IAsyncEnumerable<MediaMatchResult> IdentifyWithDetailsAsync(IAsyncEnumerable<IdentifyRequest> requests, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaIdentity>> RegisterAsync(
		IReadOnlyList<IReadOnlyList<IExternalMediaId>> candidateGroups,
		CancellationToken cancellationToken = default
	);

	Task InvalidateAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<MediaId, RatingSummary>> GetStoredRatingsAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetSinkIdsAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);
}

public record IdentifyRequest(IReadOnlyList<IExternalMediaId> Candidates, Optional<IdentifyHints> Hints)
{
	public MediaType Type => Candidates.Count == 0 ? MediaType.Unknown : Candidates[0].Type;
}
