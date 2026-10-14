using Shared.DI;

namespace UI.Features.Settings.Components.About;

internal sealed class AboutModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton(new ThirdPartyNotices(AppContext.BaseDirectory));
	}
}
