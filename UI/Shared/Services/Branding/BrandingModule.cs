using Shared.DI;

namespace UI.Shared.Services.Branding;

internal sealed class BrandingModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<MediaProviderBrandingService>();
	}
}
