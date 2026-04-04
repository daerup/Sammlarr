using BlazorBlueprint.Components;
using Shared.DI;
using UI.Shared.Services;

namespace UI;

internal class UiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddRazorComponents().AddInteractiveServerComponents();
		services.AddBlazorBlueprintComponents();
		services.AddScoped<ScrollService>();
		services.AddScoped<ProfileDropdownService>();
	}
}
