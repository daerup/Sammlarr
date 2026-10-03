using Shared.DI;
using UI.Features.Settings;

namespace UI.Features;

internal class FeaturesModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<SettingsModule>();
	}
}
