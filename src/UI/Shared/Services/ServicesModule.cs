using Shared.DI;
using Shared.Options;
using UI.Shared.Branding.Birds;
using UI.Shared.Media;
using UI.Shared.Services.Branding;
using UI.Shared.Services.Links;

namespace UI.Shared.Services;

internal sealed class ServicesModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<BrandingModule>();
		services.AddModule<LinksModule>();
		services.AddScoped<ScrollService>();
		services.AddScoped<Notifier>();
		services.AddScoped<ProfileDropdownService>();
		services.AddScoped<AccountConnectionPrompt>();
		services.AddScoped<SearchLauncher>();
		services.AddScoped<PersonPainter>();
		services.AddScoped<ArtworkSources>();
		services.AddScoped<RatingContext>();
		services.AddScoped<ExternalIdsView>();
		services.AddSingleton(provider => new AppVersionProvider(provider.GetRequiredService<ReleaseOptions>(), AppVersion.Running()));
		services.AddSingleton<AppUpdateChecker>();
	}
}
