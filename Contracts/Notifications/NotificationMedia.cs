using Domain.Media;

namespace Contracts.Notifications;

public sealed record NotificationMedia(string Id, string? Title, MediaType Type, int? Year = null, string? Descriptor = null);
