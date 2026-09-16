using Domain.Notifications;

namespace Contracts.Notifications;

public interface INotificationTemplateRenderer
{
	Result<RenderedNotification> Render(NotificationTemplate template, NotificationContext context);
}
