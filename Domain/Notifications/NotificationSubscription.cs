namespace Domain.Notifications;

public sealed record NotificationSubscription(Guid Id, Guid TargetId, string EventType);
