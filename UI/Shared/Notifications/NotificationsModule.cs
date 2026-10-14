using Shared.DI;
using UI.Shared.Notifications.WebUi;

namespace UI.Shared.Notifications;

internal sealed class NotificationsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<WebUiModule>();
	}
}
