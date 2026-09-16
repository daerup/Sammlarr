using Domain.Notifications;

namespace UI.Shared.Services;

internal sealed record Toast(NotificationLevel Level, string Title, string Message);
