using Domain.Notifications;

namespace Contracts.Notifications;

public sealed record NotificationContext(
	Guid Id,
	NotificationEventType Type,
	NotificationLevel DefaultLevel,
	DateTimeOffset OccurredAt,
	string Title,
	string Summary,
	NotificationData Data
);
