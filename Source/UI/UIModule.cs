using BlazorBlueprint.Components;
using Shared.DI;
using UI.Shared;

namespace UI;

public class UiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddRazorComponents().AddInteractiveServerComponents();
		services.AddBlazorBlueprintComponents();
		services.AddScoped<ProfileDropdownService>();
	}
}
