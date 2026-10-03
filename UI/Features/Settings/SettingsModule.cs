using Shared.DI;
using UI.Features.Settings.Components;

namespace UI.Features.Settings;

internal class SettingsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<ComponentsModule>();
	}
}
