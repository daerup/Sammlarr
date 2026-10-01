using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Outbox;

namespace Contracts.Wantlist;

public interface IMediaSink
{
	SinkKind Kind { get; }

	Task<SinkContents> ReadAsync(CancellationToken cancellationToken = default);

	IAsyncEnumerable<SinkSyncAck> ApplyAsync(WantlistOperationAction action, IReadOnlyList<MediaId> mediaIds, CancellationToken cancellationToken = default);
}

public sealed record SinkContents(IReadOnlyList<MediaId> Matched, IReadOnlyList<UnmatchedMedia> Unmatched);

public readonly record struct SinkSyncAck(MediaId MediaId, SinkSyncOutcome Outcome);

public enum SinkSyncOutcome
{
	Applied,
	Failed,
	Unaddressable,
}
