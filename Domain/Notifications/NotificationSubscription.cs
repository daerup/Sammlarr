namespace Domain.Notifications;

public sealed record NotificationSubscription(Guid Id, Guid TargetId, IReadOnlyList<NotificationEventType> EventTypes, NotificationTemplate Template);
