using Domain.Identity;
using Domain.Notifications;

namespace Domain.Announcing;

public interface INotification
{
	Guid Id { get; }

	NotificationEventType Type { get; }

	UserId UserId { get; }

	DateTimeOffset OccurredAt { get; }

	string Summary { get; }

	NotificationLevel DefaultLevel { get; }
}
