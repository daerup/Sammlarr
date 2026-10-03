using Shared.DI;
using UI.Features.Settings.Components.Notifications;

namespace UI.Features.Settings.Components;

internal class ComponentsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<NotificationsModule>();
	}
}
