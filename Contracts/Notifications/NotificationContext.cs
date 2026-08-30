namespace Contracts.Notifications;

public sealed record NotificationContext(Guid Id, string Type, DateTimeOffset OccurredAt, string Title, string Summary, NotificationData Data);
