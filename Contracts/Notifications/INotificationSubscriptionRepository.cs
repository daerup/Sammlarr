using Domain.Notifications;

namespace Contracts.Notifications;

public interface INotificationSubscriptionRepository
{
	Task<IReadOnlyList<NotificationSubscription>> GetForTargetAsync(Guid targetId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<NotificationSubscription>> GetForEventTypeAsync(string eventType, CancellationToken cancellationToken = default);

	Task<Guid> AddAsync(Guid targetId, IReadOnlyList<string> eventTypes, NotificationTemplate template, CancellationToken cancellationToken = default);

	Task UpdateAsync(Guid id, IReadOnlyList<string> eventTypes, NotificationTemplate template, CancellationToken cancellationToken = default);

	Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
