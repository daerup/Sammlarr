using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Outbox;

namespace Contracts.Wantlist.Outbox;

public interface IWantlistOutbox
{
	Task<IReadOnlyList<WantlistOutboxEntry>> GetAllAsync(SinkKind sink, CancellationToken cancellationToken = default);

	Task EnqueueAsync(SinkKind sink, WantlistOperationAction action, IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	Task ReplaceAsync(SinkKind sink, IEnumerable<MediaId> adds, CancellationToken cancellationToken = default);

	Task DropAsync(SinkKind sink, IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	Task DropOtherSinksAsync(SinkKind sink, CancellationToken cancellationToken = default);

	Task RecordFailedAttemptAsync(
		SinkKind sink,
		IEnumerable<MediaId> ids,
		WantlistOutboxState resultingState,
		Optional<string> reason,
		CancellationToken cancellationToken = default
	);
}
