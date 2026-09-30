using Domain.Media;
using Domain.Wantlist.Outbox;

namespace Contracts.Wantlist.Outbox;

public interface IWantlistOutbox
{
	Task<IReadOnlyList<WantlistOutboxEntry>> GetAsync(CancellationToken cancellationToken = default);

	Task EnqueueAsync(WantlistOperationAction action, IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);

	Task<WantlistOutboxEntry?> FindPendingAsync(MediaId mediaId, CancellationToken cancellationToken = default);

	Task DropAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);

	Task RecordFailedAttemptAsync(IEnumerable<MediaId> mediaIds, WantlistOutboxState resultingState, CancellationToken cancellationToken = default);
}
