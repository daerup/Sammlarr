namespace Domain.Notifications;

public sealed record NotificationSubscription(Guid Id, Guid TargetId, IReadOnlyList<string> EventTypes, NotificationTemplate Template);
