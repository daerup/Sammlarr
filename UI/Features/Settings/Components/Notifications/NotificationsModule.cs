using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace UI.Features.Settings.Components.Notifications;

internal class NotificationsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<NotificationTester>();
	}
}
