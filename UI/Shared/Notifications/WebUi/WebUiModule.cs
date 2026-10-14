using Contracts.Notifications;
using Domain.Notifications;
using Shared.DI;

namespace UI.Shared.Notifications.WebUi;

internal sealed class WebUiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<WebUiCircuits>();
		services.AddKeyedScoped<INotificationChannel, WebUiChannel>(NotificationChannel.WebUi);
	}
}
