using BlazorBlueprint.Components;
using Contracts.Identity;
using Observability;
using Shared.DI;
using UI.Shared.Identity;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

namespace UI;

internal class UiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<ObservabilityModule>();
		services.AddModule<UI.Observability.InstrumentationModule>();
		services.AddModule<AuthenticationModule>();
		services.AddModule<HealthModule>();
		services.AddRazorComponents().AddInteractiveServerComponents();
		services.AddBlazorBlueprintComponents();
		services.AddScoped<CurrentUserAccessor>();
		services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUserAccessor>());
		services.AddScoped<ScrollService>();
		services.AddScoped<Notifier>();
		services.AddScoped<ProfileDropdownService>();
		services.AddScoped<AccountConnectionPrompt>();
		services.AddScoped<MediaProviderBrandingService>();
		services.AddScoped<UI.Features.Settings.Components.Notifications.NotificationTester>();
	}
}
