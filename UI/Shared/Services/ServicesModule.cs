using Shared.DI;
using UI.Shared.Services.Branding;

namespace UI.Shared.Services;

internal class ServicesModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<BrandingModule>();
		services.AddScoped<ScrollService>();
		services.AddScoped<Notifier>();
		services.AddScoped<ProfileDropdownService>();
		services.AddScoped<AccountConnectionPrompt>();
		services.AddScoped<ArtworkSources>();
		services.AddSingleton<AppVersionProvider>();
	}
}
