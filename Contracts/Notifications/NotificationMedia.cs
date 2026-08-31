namespace Contracts.Notifications;

public sealed record NotificationMedia(string Id, string? Title, string Type, int? Year = null, string? Descriptor = null);
