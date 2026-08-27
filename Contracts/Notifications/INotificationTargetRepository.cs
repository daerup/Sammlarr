using Domain.Notifications;

namespace Contracts.Notifications;

public interface INotificationTargetRepository
{
	Task<IReadOnlyList<NotificationTarget>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<NotificationTarget?> FindAsync(Guid id, CancellationToken cancellationToken = default);

	Task<Guid> CreateAsync(NotificationTarget target, CancellationToken cancellationToken = default);

	Task UpdateAsync(NotificationTarget target, CancellationToken cancellationToken = default);

	Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
