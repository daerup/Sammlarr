using BlazorBlueprint.Components;
using Contracts.Identity;
using Contracts.Notifications;
using Domain.Notifications;
using Observability;
using Shared.DI;
using UI.Shared.Identity;
using UI.Shared.Notifications.WebUi;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

namespace Web;

internal class UiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<ObservabilityModule>();
		services.AddModule<InstrumentationModule>();
		services.AddModule<AuthenticationModule>();
		services.AddModule<HealthModule>();
		services.AddRazorComponents().AddInteractiveServerComponents();
		services.AddBlazorBlueprintComponents();
		services.AddScoped<CurrentUserAccessor>();
		services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUserAccessor>());
		services.AddScoped<ScrollService>();
		services.AddScoped<Notifier>();
		services.AddSingleton<WebUiCircuits>();
		services.AddKeyedScoped<INotificationChannel, WebUiChannel>(NotificationChannel.WebUi);
		services.AddScoped<ProfileDropdownService>();
		services.AddScoped<AccountConnectionPrompt>();
		services.AddScoped(sp => new MediaProviderBrandingService(sp.GetRequiredService<IWebHostEnvironment>().WebRootPath));
		services.AddScoped<UI.Features.Settings.Components.Notifications.NotificationTester>();
	}
}
