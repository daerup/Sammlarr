using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace UI.Shared.Notifications.WebUi;

internal class WebUiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<WebUiCircuits>();
		services.AddKeyedScoped<INotificationChannel, WebUiChannel>(NotificationChannel.WebUi);
	}
}
