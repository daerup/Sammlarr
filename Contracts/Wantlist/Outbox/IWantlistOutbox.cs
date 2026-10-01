using Domain.Media;
using Domain.Wantlist.Outbox;

namespace Contracts.Wantlist.Outbox;

public interface IWantlistOutbox
{
	Task<IReadOnlyList<WantlistOutboxEntry>> GetAllAsync(CancellationToken cancellationToken = default);

	Task EnqueueAsync(WantlistOperationAction action, IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	Task DropAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default);

	Task RecordFailedAttemptAsync(IEnumerable<MediaId> ids, WantlistOutboxState resultingState, CancellationToken cancellationToken = default);
}
