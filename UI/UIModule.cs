using BlazorBlueprint.Components;
using Shared.DI;
using UI.Shared.Services;
using UI.Shared.Services.Branding;
using UI.Telemetry;

namespace UI;

internal class UiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<TelemetryModule>();
		services.AddRazorComponents().AddInteractiveServerComponents();
		services.AddBlazorBlueprintComponents();
		services.AddScoped<ScrollService>();
		services.AddScoped<Notifier>();
		services.AddScoped<ProfileDropdownService>();
		services.AddScoped<MediaProviderBrandingService>();
	}
}
