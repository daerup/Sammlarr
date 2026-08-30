using Domain.Notifications;

namespace Contracts.Notifications;

public interface INotificationTemplateRenderer
{
	Result<string> Render(string template, NotificationContext context);
}
