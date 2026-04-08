using Contracts;
using Core.Caching;
using Core.DI;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core;

public class CoreModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<CachingModule>();
		services.AddModule<DiModule>();
		services.AddTransient<IDateTimeProvider, SystemDateTimeProvider>();
	}
}
