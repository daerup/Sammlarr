namespace Domain.Notifications;

public sealed record NotificationDeliveryOutcome(DeliveryOutcome Outcome, NotificationEventType EventType, DateTimeOffset RecordedAt);
