using Domain.Notifications;

namespace Contracts.Notifications;

public interface INotificationDeliveryOutcomeRepository
{
	Task RecordAsync(Guid eventId, Guid targetId, NotificationEventType eventType, DeliveryOutcome outcome, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<Guid, IReadOnlyList<NotificationDeliveryOutcome>>> GetRecentByTargetAsync(int count, CancellationToken cancellationToken = default);
}
