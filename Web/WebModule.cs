using BlazorBlueprint.Components;
using Shared.DI;
using UI.Shared.Services.Branding;
using Web.Auth;
using Web.Health;
using Web.Instrumentation;

namespace Web;

internal class WebModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<AuthenticationModule>();
		services.AddModule<HealthModule>();
		services.AddModule<InstrumentationModule>();
		services.AddRazorComponents().AddInteractiveServerComponents();
		services.AddBlazorBlueprintComponents();
		services.AddSingleton(sp => new BrandingAssetRoot(sp.GetRequiredService<IWebHostEnvironment>().WebRootPath));
	}
}
