using Shared.DI;
using UI.Features.Feeds;
using UI.Features.Settings;

namespace UI.Features;

internal sealed class FeaturesModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<FeedsModule>();
		services.AddModule<SettingsModule>();
	}
}
