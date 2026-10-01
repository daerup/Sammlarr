using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;

namespace Contracts.Wantlist;

public interface IMediaSink
{
	SinkKind Kind { get; }

	Task<SinkSnapshot> GetAsync(CancellationToken cancellationToken = default);

	IAsyncEnumerable<SinkSyncAck> ApplyAsync(WantlistOperationAction action, IReadOnlyList<MediaId> ids, CancellationToken cancellationToken = default);
}

public readonly record struct SinkSyncAck(MediaId MediaId, SinkSyncOutcome Outcome);

public enum SinkSyncOutcome
{
	Applied,
	Failed,
	Unaddressable,
}
