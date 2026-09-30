using Domain.Media;

namespace Contracts.Wantlist;

public interface IMediaSink
{
	IAsyncEnumerable<UnmatchedMedia> GetWantlistAsync(CancellationToken cancellationToken = default);

	IAsyncEnumerable<SinkSyncAck> AddToWantlistAsync(IEnumerable<IExternalMediaId> ids, CancellationToken cancellationToken = default);

	IAsyncEnumerable<SinkSyncAck> RemoveFromWantlistAsync(IEnumerable<IExternalMediaId> ids, CancellationToken cancellationToken = default);
}

public readonly record struct SinkSyncAck(IExternalMediaId ExternalId, bool Success);
