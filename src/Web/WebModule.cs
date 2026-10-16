using BlazorBlueprint.Components;
using Shared.DI;
using Web.Auth;
using Web.Health;
using Web.Instrumentation;
using Web.RateLimiting;

namespace Web;

internal sealed class WebModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<AuthenticationModule>();
		services.AddModule<HealthModule>();
		services.AddModule<InstrumentationModule>();
		services.AddModule<RateLimitingModule>();
		services.AddRazorComponents().AddInteractiveServerComponents();
		services.AddBlazorBlueprintComponents();
	}
}
