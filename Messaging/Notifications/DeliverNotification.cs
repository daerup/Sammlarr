using Contracts.Notifications;
using Domain.Identity;

namespace Messaging.Notifications;

public sealed record DeliverNotification(NotificationContext Context, Guid TargetId, UserId UserId);
