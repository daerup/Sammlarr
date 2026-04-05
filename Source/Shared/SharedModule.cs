using Microsoft.Extensions.DependencyInjection;
using Shared.Caching;
using Shared.DI;
using Shared.Options;

namespace Shared;

public class SharedModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<CachingModule>();
		services.AddModule<DiModule>();
		services.AddModule<OptionsModule>();
	}
}
