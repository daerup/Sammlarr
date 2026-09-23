using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using UI.Shared.Identity;
using UI.Shared.Notifications;
using UI.Shared.Services;

namespace UI.Shared;

internal class UiSharedModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<IdentityModule>();
		services.AddModule<NotificationsModule>();
		services.AddModule<ServicesModule>();
	}
}
