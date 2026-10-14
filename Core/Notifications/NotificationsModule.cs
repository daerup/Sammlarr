using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core.Notifications;

internal sealed class NotificationsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<WebUiNotificationSeeder>();
	}
}
