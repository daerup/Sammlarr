using BlazorBlueprint.Components;
using Observability;
using Shared.DI;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

namespace UI;

internal class UiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<ObservabilityModule>();
		services.AddModule<UI.Observability.InstrumentationModule>();
		services.AddRazorComponents().AddInteractiveServerComponents();
		services.AddBlazorBlueprintComponents();
		services.AddScoped<ScrollService>();
		services.AddScoped<Notifier>();
		services.AddScoped<ProfileDropdownService>();
		services.AddScoped<MediaProviderBrandingService>();
	}
}
