using Shared.DI;
using UI.Features;
using UI.Shared;

namespace UI;

public class UiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<FeaturesModule>();
		services.AddModule<UiSharedModule>();
	}
}
