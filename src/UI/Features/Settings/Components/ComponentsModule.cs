using Shared.DI;
using UI.Features.Settings.Components.About;
using UI.Features.Settings.Components.Notifications;

namespace UI.Features.Settings.Components;

internal sealed class ComponentsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<AboutModule>();
		services.AddModule<NotificationsModule>();
	}
}
