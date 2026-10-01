using Domain.Media;
using Domain.Providers;

namespace Contracts.Wantlist;

public interface IMediaSink
{
	SinkKind Kind { get; }

	Task<SinkContents> ReadAsync(CancellationToken cancellationToken = default);

	IAsyncEnumerable<SinkSyncAck> AddToWantlistAsync(IEnumerable<IExternalMediaId> ids, CancellationToken cancellationToken = default);

	IAsyncEnumerable<SinkSyncAck> RemoveFromWantlistAsync(IEnumerable<IExternalMediaId> ids, CancellationToken cancellationToken = default);
}

public sealed record SinkContents(IReadOnlyList<MediaId> Matched, IReadOnlyList<UnmatchedMedia> Unmatched);

public readonly record struct SinkSyncAck(IExternalMediaId ExternalId, bool Success);
