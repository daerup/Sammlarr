using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace UI.Shared.Services.Branding;

internal class BrandingModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped(sp => new MediaProviderBrandingService(sp.GetRequiredService<BrandingAssetRoot>().Path));
	}
}
