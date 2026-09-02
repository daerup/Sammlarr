namespace Domain.Notifications;

public sealed record NotificationDeliveryOutcome(DeliveryOutcome Outcome, string EventType, DateTimeOffset RecordedAt);
