using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;

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

	Task<IReadOnlyDictionary<MediaId, MediaRatings>> GetStoredRatingsAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetSinkIdsAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetSinkIdsAsync(IEnumerable<MediaId> ids, SinkKind sink, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<MediaId>> EnsureSinkIdsAsync(IEnumerable<MediaId> ids, SinkKind sink, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<MediaId, MediaType>> GetTypesAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	Task<Optional<IExternalMediaId>> FindIdAsync(MediaId id, MediaProvider provider, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetIdsAsync(
		IEnumerable<MediaId> ids,
		MediaProvider provider,
		CancellationToken cancellationToken = default
	);
}

public record IdentifyRequest(IReadOnlyList<IExternalMediaId> Candidates, Optional<IdentifyHints> Hints)
{
	public MediaType Type => Candidates.Count == 0 ? MediaType.Unknown : Candidates[0].Type;

	public Optional<IdentifyHints> Description { get; init; } = new None();
}
